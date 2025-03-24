//css_ref IconExtractor.dll
using System;
using System.Diagnostics;
using System.Drawing;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using mkshim;
using Toolbelt.Drawing;

static class MkShim
{
    static StringBuilder compileLog = new StringBuilder();

    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        main(args);
    }

    static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        return
            args.Name.Contains("System.Buffers") ? Assembly.Load(Resource1.System_Buffers) :
            args.Name.Contains("System.Collections.Immutable") ? Assembly.Load(Resource1.System_Collections_Immutable) :
            args.Name.Contains("System.Memory") ? Assembly.Load(Resource1.System_Memory) :
            args.Name.Contains("System.Numerics.Vectors") ? Assembly.Load(Resource1.System_Numerics_Vectors) :
            args.Name.Contains("System.Reflection.Metadata") ? Assembly.Load(Resource1.System_Reflection_Metadata) :
            args.Name.Contains("System.Runtime.CompilerServices.Unsafe") ? Assembly.Load(Resource1.System_Runtime_CompilerServices_Unsafe) :
            args.Name.Contains("IconExtractor") ? Assembly.Load(Resource1.IconExtractor) :
            null;
    }

    static void main(string[] args)
    {
        if (HandleUserInput(args))
            return;

        var shim = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[0]));
        var exe = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]));

        if (!File.Exists(exe))
            throw new FileNotFoundException(exe);

        var defaultArgs = (args.ArgValue("-p") ?? args.ArgValue("--params"))?
                           .Replace("\\", "\\\\")
                           .Replace("\"", "\\\"")
                           ?? "";

        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"You mast specify the executable file to create the shim for.");
            return;
        }

        var buildDir = Path.Combine(Path.GetTempPath(), $"mkshim-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(buildDir);

            (bool isWinApp, bool is64App) = exe.GetPeInfo();

            var icon = exe.ExtractFirstIconToFolder(buildDir);
            var csFile = exe.GetShimSourceCodeFor(buildDir, isWinApp, defaultArgs);
            var res = exe.GenerateResFor(buildDir, defaultArgs, icon);

            var appRes = (res != null ? $"/win32res:\"{res}\"" : "");
            var cpu = (is64App ? "/platform:x64" : "");

            var build = csc.Run($"-out:\"{shim}\" {appRes} {cpu} /target:{(isWinApp ? "winexe" : "exe")} \"{csFile}\"");
            build.WaitForExit();

            if (build.ExitCode == 0)
            {
                Console.WriteLine($"The shim has been created");
                Console.WriteLine($"  {shim}");
                Console.WriteLine($"     `-> {exe}");
            }
            else
            {
                Console.WriteLine($"Cannot build the shim.");
                Console.WriteLine($"Error: ");
                Console.WriteLine(compileLog);
            }
        }
        finally { try { Directory.Delete(buildDir, true); } catch { } }
    }

    static bool HandleUserInput(string[] args)
    {
        if (args.Contains("-h") || args.Contains("-?") || args.Contains("?") || args.Contains("-help"))
        {
            Console.WriteLine($@"{Path.GetFileNameWithoutExtension(ThisAssemblyFile)} (v{ThisAssemblyFileVersion}) - Shim generator");
            Console.WriteLine("Copyright(C) 2024 - 2025 Oleg Shilo (github.com/oleg-shilo)");
            Console.WriteLine($@"Generates shim for a given executable file.");
            Console.WriteLine();
            Console.WriteLine($@"Usage:");
            Console.WriteLine($@"   mkshim <shim_name> <target_executable> [--params:<args>]");
            Console.WriteLine();
            Console.WriteLine("-v | --version");
            Console.WriteLine("    Prints mkshim version.");
            Console.WriteLine("-p:args | --params:args");
            Console.WriteLine("    The default arguments you always want to pass to the target executable.");
            Console.WriteLine("    IE with chrome.exe shim: 'chrome.exe --save-page-as-mhtml --user-data-dir=\"/some/path\"'");
            Console.WriteLine();
            Console.WriteLine("You can use special mkshim arguments with the created shim:");
            Console.WriteLine(" --mkshim-noop");
            Console.WriteLine("   Execute created shim but print <target_executable> instead of executing it.");
            Console.WriteLine(" --mkshim-test");
            Console.WriteLine("   Tests if shim's <target_executable> exists.");
            return true;
        }
        else if (args.Contains("-v") || args.Contains("--version") || args.Contains("-version"))
        {
            Console.WriteLine(ThisAssemblyFileVersion);
            return true;
        }

        if (!args.Any() || args.Count() < 2)
        {
            Console.WriteLine($@"Not enough arguments were specified. Execute 'mkshim -?' for usage help.");
            return true;
        }

        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Console.WriteLine("Creating a shim to an executable file this way is only useful on Windows. On Linux you " +
                              "have a much better option `alias`. You can use it as in the example for CS-Script executable below: " + Environment.NewLine +
                              "alias css='dotnet /usr/local/bin/cs-script/cscs.exe'" + Environment.NewLine +
                              "After that you can invoke CS-Script engine from anywhere by just typing 'css'.");
            return true;
        }

        return false;
    }

    static string ExtractFirstIconToFolder(this string binFilePath, string outDir)
    {
        try
        {
            string iconFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(binFilePath) + ".ico");
            using (var s = File.Create(iconFile))
                IconExtractor.Extract1stIconTo(binFilePath, s);

            using (var validIcon = Bitmap.FromFile(iconFile)) // check that it is a valid icon file
            { }

            return iconFile;
        }
        catch { }
        return null;
    }

    static (bool isWin, bool is64) GetPeInfo(this string exe)
    {
        using (var stream = File.OpenRead(exe))
        using (var peFile = new PEReader(stream))
            return (!peFile.PEHeaders.IsConsoleApplication, peFile.PEHeaders.CoffHeader.Machine == Machine.Amd64);
    }

    public static string ArgValue(this string[] args, string name)
    {
        return args.FirstOrDefault(x => x.StartsWith($"{name}:"))?.Split(new[] { ':' }, 2).LastOrDefault();
    }

    static string GetShimSourceCodeFor(this string exe, string outDir, bool isWinApp, string defaultArgs)
    {
        var version = exe.GetFileVersion().FileVersion;
        var template = Encoding.Default.GetString(Resource1.ConsoleShim);
        var csFile = Path.Combine(outDir, Path.GetFileName(exe) + ".cs");

        var code = template.Replace("//{version}", $"[assembly: System.Reflection.AssemblyFileVersionAttribute(\"{version}\")]")
                           .Replace("//{target}", $"[assembly: System.Reflection.AssemblyDescriptionAttribute(@\"Shim to {exe}\")]")
                           .Replace("//{appFile}", $"static string appFile = @\"{exe}\";")
                           .Replace("//{defaultArgs}", $"static string defaultArgs = \"{defaultArgs} \";")
                           .Replace("//{waitForExit}", $"var toWait = {(isWinApp ? "false" : "true")};");

        File.WriteAllText(csFile, code);

        return csFile;
    }

    static Process Run(this string exe, string args)
    {
        var p = new Process();
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.UseShellExecute = false;
        // ChildProcess.StartInfo.WorkingDirectory = workingDir;
        p.StartInfo.RedirectStandardError = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardInput = false;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

        p.Start();

        string line;
        while (null != (line = p.StandardOutput.ReadLine()))
        {
            if (line.Trim() != "" && !line.Trim().StartsWith("This compiler is provided as part of the Microsoft (R) .NET Framework,"))
                compileLog.AppendLine("> " + line);
        }
        return p;
    }

    // static string GetFileVersion(this string file)
    //     => FileVersionInfo.GetVersionInfo(file).FileVersion;
    static FileVersionInfo GetFileVersion(this string file)
        => FileVersionInfo.GetVersionInfo(file);

    static string ThisAssemblyFile => Assembly.GetExecutingAssembly().Location;
    static string ThisAssemblyFileVersion => FileVersionInfo.GetVersionInfo(ThisAssemblyFile).FileVersion;

    static string _rc;

    static string rc
    {
        get
        {
            if (_rc == null)
            {
                var dir = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData), "mkshim", $"v{ThisAssemblyFileVersion}");
                Directory.CreateDirectory(dir); // will not fail if exists

                ClearOldBinaries(dir);

                var rc_exe = Path.Combine(dir, "rc.exe");
                var rc_dll = Path.Combine(dir, "rcdll.dll");
                var servicing_dll = Path.Combine(dir, "ServicingCommon.dll");

                if (!File.Exists(rc_exe))
                {
                    // write file from resources
                    File.WriteAllBytes(rc_exe, Resource1.rc_exe);
                    File.WriteAllBytes(rc_dll, Resource1.rcdll);
                    File.WriteAllBytes(servicing_dll, Resource1.ServicingCommon);
                }

                _rc = rc_exe;
            }
            return _rc;
        }
    }

    static void ClearOldBinaries(string thisAppBinaryDir)
    {
        var rootDir = Path.GetDirectoryName(thisAppBinaryDir);
        // v1.1.0.0
        foreach (var item in Directory.GetFiles(rootDir, "*.*"))
            try
            {
                File.Delete(item);
            }
            catch { }

        // v1.1.0.0+
        foreach (var item in Directory.GetDirectories(rootDir, "v*.*.*").Where(x => x != thisAppBinaryDir))
            try
            {
                Directory.Delete(item, recursive: true);
            }
            catch { }
    }

    static string csc
    {
        get
        {
            var location = Path.Combine(Path.GetDirectoryName("".GetType().Assembly.Location), "csc.exe");

            if (File.Exists(location))
                return location;

            location = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
            Console.WriteLine("Cannot locate csc.exe. Trying its default location: " + location);
            return location;
        }
    }

    static string EscapeCSharpPath(this string path) => path.Replace("\\", "\\\\");

    static string GenerateResFor(this string targetExe, string outDir, string defaultArgs, string iconFile)
    {
        string rcFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(targetExe) + ".rc");
        string resFile = Path.ChangeExtension(rcFile, ".res");
        var targetFileMetadata = targetExe.GetFileVersion();
        var defArgs = string.IsNullOrEmpty(defaultArgs) ? "" : $"; Default params: {defaultArgs.Replace("\\\"", "'")}"; // sigcheck does not like `\"` in the res value text

        // A simplest possible RC file content
        string rcContent = $@"
1 VERSIONINFO
FILEVERSION 1,0,0,0
PRODUCTVERSION 1,0,0,0
BEGIN
    BLOCK ""StringFileInfo""
    BEGIN
        BLOCK ""040904B0""  // Language: US English
        BEGIN
            VALUE ""FileDescription"", ""Shim to {Path.GetFileName(targetExe)} (created with mkshim v{Assembly.GetExecutingAssembly().GetName().Version}){defArgs}""
            VALUE ""FileVersion"", ""{targetFileMetadata.FileVersion}""
            VALUE ""ProductVersion"", ""{targetFileMetadata.ProductVersion}""
            VALUE ""ProductName"", ""{targetExe.EscapeCSharpPath()}""
        END
    END
    BLOCK ""VarFileInfo""
    BEGIN
        VALUE ""Translation"", 0x0409, 1200
    END
END
";
        if (!string.IsNullOrEmpty(iconFile))
        {
            rcContent += $@"
1 ICON ""{iconFile.EscapeCSharpPath()}""
IDI_MAIN_ICON
";
        }

        File.WriteAllText(rcFile, rcContent);

        // Compile the RC file to a RES file
        var build = rc.Run(rcFile);

        build.WaitForExit();

        return build.ExitCode == 0 ? resFile : null;
    }
}