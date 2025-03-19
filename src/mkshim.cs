//css_ref IconExtractor.dll
using System;
using System.Diagnostics;
using System.Drawing;
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

        var shim = Path.GetFullPath(args[0]);
        var exe = Path.GetFullPath(args[1]);

        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"You mast specify the executable file to create the shim for.");
            return;
        }

        var buildDir = Path.Combine(Path.GetTempPath(), $"mkshim-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(buildDir);

            var isWinApp = exe.IsWindowExe();
            var icon = exe.ExtractFirstIconToFolder(buildDir);
            var csFile = exe.GetShimSourceCodeFor(buildDir, isWinApp);

            var appIcon = (icon != null ? $"/win32icon:\"{icon}\"" : "");

            var build = csc.Run($"-out:\"{shim}\" {appIcon} /target:{(isWinApp ? "winexe" : "exe")} \"{csFile}\"");
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
            Console.WriteLine($@"{Path.GetFileName(ThisAssemblyFile)} (v{ThisAssemblyFileVersion})");
            Console.WriteLine($@"Generates shim for a given executable file.");
            Console.WriteLine($@"Usage:");
            Console.WriteLine($@"   mkshim <shim_name> <mapped_executable>");
            return true;
        }

        if (args.Contains("-v") || args.Contains("-version"))
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
            Console.WriteLine("Creating a shim to an executable file this way is only useful on windows. On Linux you " +
                              "have a much better option `alias`. You can enable it as below: " + Environment.NewLine +
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

    static bool IsWindowExe(this string exe)
    {
        using (var stream = File.OpenRead(exe))
        using (var peFile = new PEReader(stream))
            return !peFile.PEHeaders.IsConsoleApplication;
    }

    public static string ArgValue(this string[] args, string name)
    {
        return args.FirstOrDefault(x => x.StartsWith($"-{name}:"))?.Split(new[] { ':' }, 2).LastOrDefault();
    }

    static string GetShimSourceCodeFor(this string exe, string outDir, bool isWinApp)
    {
        var version = exe.GetFileVersion();
        var template = Encoding.Default.GetString(Resource1.ConsoleShim);
        var csFile = Path.Combine(outDir, Path.GetFileName(exe) + ".cs");

        var code = template.Replace("//{version}", $"[assembly: System.Reflection.AssemblyFileVersionAttribute(\"{version}\")]")
                           .Replace("//{appFile}", $"static string appFile = @\"{exe}\";")
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

    static string GetFileVersion(this string file)
        => FileVersionInfo.GetVersionInfo(file).FileVersion;

    static string ThisAssemblyFile => Assembly.GetExecutingAssembly().Location;
    static string ThisAssemblyFileVersion => ThisAssemblyFile.GetFileVersion();

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
}