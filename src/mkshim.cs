//css_ref IconExtractor.dll
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using static System.Environment;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Xml.Schema;
using mkshim;

// using Toolbelt.Drawing;
using TsudaKageyu;

static class MkShim
{
    static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, arg) =>
                      arg.Name.Contains("System.Buffers") ? Assembly.Load(Resource1.System_Buffers) :
                      arg.Name.Contains("System.Collections.Immutable") ? Assembly.Load(Resource1.System_Collections_Immutable) :
                      arg.Name.Contains("System.Memory") ? Assembly.Load(Resource1.System_Memory) :
                      arg.Name.Contains("System.Numerics.Vectors") ? Assembly.Load(Resource1.System_Numerics_Vectors) :
                      arg.Name.Contains("System.Reflection.Metadata") ? Assembly.Load(Resource1.System_Reflection_Metadata) :
                      arg.Name.Contains("System.Runtime.CompilerServices.Unsafe") ? Assembly.Load(Resource1.System_Runtime_CompilerServices_Unsafe) :
                      null;

            // `main` needs to be in a separate method so premature assembly loading is avoided
            main(args);
        }
        catch (ValidationException e)
        {
            Console.WriteLine(e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    static void main(string[] args)
    {
        RunOptions options = args.Parse().Validate();

        if (HandleNonRunable(options))
            return;

        var buildDir = Path.Combine(Path.GetTempPath(), $"mkshim-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(buildDir);

            (bool isWinApp, bool is64App) = options.TargetExecutable.GetPeInfo();

            if (options.NoConsole)
                isWinApp = true; // force windows app if no console option is specified

            var icon =
                options.IconFile ??
                options.TargetExecutable.LookupPackageIcon() ??
                options.TargetExecutable.ExtractFirstIconToFolder(buildDir, handeErrors: true) ??
                options.TargetExecutable.ExtractDefaultAppIconToFolder(buildDir);

            if (icon?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
            {
                icon = icon.ExtractFirstIconToFolder(buildDir);
            }

            if (!options.NoOverlay)
                icon = icon?.ApplyOverlayToIcon(icon.ChangeDir(buildDir));

            var targetRuntimePath = options.TargetExecutable;
            if (options.RelativeTargetPath)
                targetRuntimePath = options.TargetExecutable.ToRelativePathFrom(options.ShimName.GetDirName());

            var csFile = options.TargetExecutable.GetShimSourceCodeFor(buildDir, isWinApp, options.DefaultArguments, targetRuntimePath);
            var manifestFile = options.ShimRequiresElevation.GetShimManifestFile(buildDir);
            var res = options.TargetExecutable.GenerateResFor(buildDir, options.DefaultArguments, icon, manifestFile, targetRuntimePath);

            if (res == null)
            {
                Console.WriteLine($"WARNING: Cannot generate shim resources with rc.exe. The shim file will have no MkShim related properties.");
                Console.WriteLine(compileLog);
                Console.WriteLine("---");
            }

            var appRes = (res != null ? $"/win32res:\"{res}\"" : "");
            var cpu = (is64App ? "/platform:x64" : "");

            var build = csc.Run($"-out:\"{options.ShimName}\" {appRes} {cpu} /target:{(isWinApp ? "winexe" : "exe")} \"{csFile}\"");
            build.WaitForExit();

            if (build.ExitCode == 0)
            {
                Console.WriteLine($"The shim has been created");
                Console.WriteLine($"  {options.ShimName}");
                Console.WriteLine($"     `-> {options.TargetExecutable}");
            }
            else
            {
                Console.WriteLine($"Cannot build the shim.");
                Console.WriteLine($"Error: ");
                Console.WriteLine(compileLog);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {(e is ApplicationException ? e.Message : e.ToString())}");
        }
        finally { try { Directory.Delete(buildDir, true); } catch { } }
    }

    static string LookupPackageIcon(this string binFilePath)
    {
        var probingDirs = new[]
        {
            Path.GetDirectoryName(binFilePath),
            Path.GetDirectoryName(Path.GetDirectoryName(binFilePath))
        };

        foreach (var dir in probingDirs)
        {
            var iconFile = Directory.GetFiles(dir, "*.ico")
                .FirstOrDefault(x => Path.GetFileName(x) == "favicon.ico" || Path.GetFileName(x) == Path.ChangeExtension(Path.GetFileName(binFilePath), ".ico"));

            if (iconFile != null)
                return iconFile;
        }

        return null;
    }

    static string ExtractDefaultAppIconToFolder(this string binFilePath, string outDir)
    {
        string iconFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(binFilePath) + ".ico");
        File.WriteAllBytes(iconFile, Resource1.app);
        return iconFile;
    }

    static string GetShimManifestFile(this bool requresElevation, string outDir)
    {
        string manifestFile = null;
        if (requresElevation)
        {
            var manifest =
@"<?xml version = ""1.0"" encoding = ""utf-8"" ?>
<assembly manifestVersion = ""1.0"" xmlns = ""urn:schemas-microsoft-com:asm.v1"" >
    <trustInfo xmlns = ""urn:schemas-microsoft-com:asm.v3"" >
    <security >
        <requestedPrivileges >
            <requestedExecutionLevel level = ""requireAdministrator"" uiAccess = ""false"" />
        </requestedPrivileges >
    </security >
    </trustInfo >
</assembly >
";
            manifestFile = Path.Combine(outDir, "app.manifest");
            File.WriteAllText(manifestFile, manifest, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        return manifestFile;
    }

    static string GetShimSourceCodeFor(this string exe, string outDir, bool isWinApp, string defaultArgs, string exeRuntimePath)
    {
        var version = exe.GetFileVersion().FileVersion;
        var template = Encoding.Default.GetString(Resource1.ConsoleShim);
        var csFile = Path.Combine(outDir, Path.GetFileName(exe) + ".cs");

        var code = template.Replace("//{version}", $"[assembly: System.Reflection.AssemblyFileVersionAttribute(\"{version}\")]")
                           .Replace("//{target}", $"[assembly: System.Reflection.AssemblyDescriptionAttribute(@\"Shim to {exeRuntimePath}\")]")
                           .Replace("//{appFile}", $"static string appFile = @\"{exeRuntimePath}\";")
                           .Replace("//{isConsoleFile}", $"static bool isConsole = {(!isWinApp ? "true" : "false")};")
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

    static string GenerateResFor(this string targetExe, string outDir, string defaultArgs, string iconFile, string manifestFile, string exeRuntimePath)
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
            VALUE ""FileDescription"", ""Shim to {Path.GetFileName(exeRuntimePath)} (created with mkshim v{Assembly.GetExecutingAssembly().GetName().Version}){defArgs}""
            VALUE ""FileVersion"", ""{targetFileMetadata.FileVersion}""
            VALUE ""ProductVersion"", ""{targetFileMetadata.ProductVersion}""
            VALUE ""ProductName"", ""{exeRuntimePath.EscapeCSharpPath()}""
        END
    END
    BLOCK ""VarFileInfo""
    BEGIN
        VALUE ""Translation"", 0x0409, 1200
    END
END

";

        if (!string.IsNullOrEmpty(manifestFile))
        {
            // 24 = Resource type (RT_MANIFEST)
            rcContent += $@"
1 24 ""app.manifest""
"
;
        }

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

    class ValidationException : Exception
    {
        public ValidationException(string message) : base(message)
        {
        }
    }

    static StringBuilder compileLog = new StringBuilder();

    static RunOptions Parse(this string[] args)
    {
        var options = new RunOptions();

        if (args.Contains("-h") || args.Contains("-?") || args.Contains("?") || args.Contains("-help"))
        {
            options.HelpRequest = true;
        }
        else if (args.Contains("-v") || args.Contains("--version") || args.Contains("-version"))
        {
            options.VersionRequest = true;
        }
        else if (args.Length < 2)
        {
            throw new ValidationException("Not enough arguments were specified. Execute 'mkshim -?' for usage help.");
        }
        else
        {
            options.ShimName = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[0])).EnsureExtension(".exe");
            options.TargetExecutable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1])).EnsureExtension(".exe");
            options.IconFile = args.ArgValue("--icon");
            options.NoOverlay = args.Contains("--no-overlay");
            options.ShimRequiresElevation = args.Contains("--elevate");
            options.NoConsole = args.Contains("--no-console") || args.Contains("-nc");
            options.RelativeTargetPath = args.Contains("--relative") || args.Contains("-r");
            options.DefaultArguments = (args.ArgValue("-p") ?? args.ArgValue("--params"))?
                                        .Replace("\\", "\\\\")
                                        .Replace("\"", "\\\"")
                                         ?? "";
        }
        return options;
    }

    static RunOptions Validate(this RunOptions options)
    {
        if (!options.IsRunable)
            return options;

        // OS
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new ValidationException("Creating a shim to an executable file this way is only useful on Windows. On Linux you " +
                                          "have a much better option `alias`. You can use it as in the example for CS-Script executable below: " + Environment.NewLine +
                                          "alias css='dotnet /usr/local/bin/cs-script/cscs.exe'" + Environment.NewLine +
                                          "After that you can invoke CS-Script engine from anywhere by just typing 'css'.");

        // target exe
        if (!options.TargetExecutable.IsValidFilePath())
            throw new ValidationException($"Target executable is not a valid path: {options.TargetExecutable}");

        if (!File.Exists(options.TargetExecutable))
            throw new ValidationException($"Target executable cannot be found at: {options.TargetExecutable}");

        if (!options.TargetExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) // parser would be normalizing files without the extension but it is still better to validate
            throw new ValidationException($"Target executable path is not an executable file: {options.TargetExecutable}");

        if (options.TargetExecutable.IsDirectory())
            throw new ValidationException($"Target executable path is not an executable file but a folder: {options.TargetExecutable}");

        if (!options.TargetExecutable.HasReadPermissions())
            throw new ValidationException($"Cannot access target executable file: {Path.GetDirectoryName(options.ShimName)}. Please check your permissions.");

        // shim
        if (!options.ShimName.IsValidFilePath())
            throw new ValidationException($"Shim is not a valid path: {options.ShimName}");

        if (!options.ShimName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) // parser would be normalizing files without the extension but it is still better to validate
            throw new ValidationException($"Shim path is not an executable file: {options.ShimName}");

        if (options.ShimName.IsDirectory())
            throw new ValidationException($"Shim path is not an executable file but a folder: {options.ShimName}");

        if (!Directory.Exists(Path.GetDirectoryName(options.ShimName)))
            throw new ValidationException($"Shim parent directory does not exist: {Path.GetDirectoryName(options.ShimName)}");

        if (!Path.GetDirectoryName(options.ShimName).HasWritePermissions())
            throw new ValidationException($"Cannot write to the directory: {Path.GetDirectoryName(options.ShimName)}. Please check your permissions.");

        // shim vs target
        if (Path.GetFullPath(options.ShimName).ToLower() == Path.GetFullPath(options.TargetExecutable).ToLower())
            throw new ValidationException($"Shim and target executable point to the same location. Please change shim path to point to the different location.");

        return options;
    }

    static bool HandleNonRunable(RunOptions options)
    {
        if (options.VersionRequest)
        {
            Console.WriteLine(ThisAssemblyFileVersion);
            return true;
        }

        if (options.HelpRequest)
        {
            Console.WriteLine($@"MkShim (v{ThisAssemblyFileVersion}) - Shim generator");
            Console.WriteLine("Copyright(C) 2024 - 2025 Oleg Shilo (github.com/oleg-shilo)");
            Console.WriteLine($@"Generates shim for a given executable file.");
            Console.WriteLine();
            Console.WriteLine($@"Usage:");
            Console.WriteLine($@"   mkshim <shim_name> <target_executable> [options]");
            Console.WriteLine();
            Console.WriteLine("shim_name");
            Console.WriteLine("    Path to the shim to be created.");
            Console.WriteLine("    The `.exe` extension will be assumed if the file path was specified without an extension.");
            Console.WriteLine();
            Console.WriteLine("target_executable");
            Console.WriteLine("    Path to the target executable to be pointed to by the created shim.");
            Console.WriteLine("    The `.exe` extension will be assumed if the file path was specified without an extension.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine();
            Console.WriteLine("--version | -v");
            Console.WriteLine("    Prints MkShim version.");
            Console.WriteLine();
            Console.WriteLine("--params:<args> | -p:<args>");
            Console.WriteLine("    The default arguments you always want to pass to the target executable.");
            Console.WriteLine("    IE with chrome.exe shim: 'chrome.exe --save-page-as-mhtml --user-data-dir=\"/some/path\"'");
            Console.WriteLine();
            Console.WriteLine("--icon:<iconfile>");
            Console.WriteLine("    The custom icon (or exe with the app icon) to be embedded in the shim. If not specified then the icon will be resolved in the following order:");
            Console.WriteLine("    1. The application package icon will be looked up in the current and parent folder.");
            Console.WriteLine("       The expected package icon name is `favicon.ico` or  `<app>.ico`.");
            Console.WriteLine("    2. The icon of the target file.");
            Console.WriteLine("    3. MkShim application icon.");
            Console.WriteLine();
            Console.WriteLine("--relative | -r");
            Console.WriteLine("    The created shim is to point to the target executable by the relative path with respect to the shim location.");
            Console.WriteLine("    Note, if the shim and the target path are pointing to the different drives the resulting path will be the absolute path to the target.");
            Console.WriteLine();
            Console.WriteLine("--no-console | -nc");
            Console.WriteLine("    No console option.");
            Console.WriteLine("    The shim will not have console attached regardless of the PE type (console vs windows) of the target executable.");
            Console.WriteLine();
            Console.WriteLine("--no-overlay");
            Console.WriteLine("    Disable embedding 'shim' overlay to the application icon of the shim executable.");
            Console.WriteLine("    By default MkShim always creates an overlay to visually distinguish the shim from the target file.");
            Console.WriteLine();
            Console.WriteLine("--elevate");
            Console.WriteLine("    Build the shim that requires elevation at startup.");
            Console.WriteLine("    By default MkShim creates the shim that does not require elevation");
            Console.WriteLine();
            Console.WriteLine("Runtime:");
            Console.WriteLine();
            Console.WriteLine("The shim always runs the target executable in a separate process");
            Console.WriteLine("You can use special MkShim arguments with the created shim:");
            Console.WriteLine(" --mkshim-noop");
            Console.WriteLine("   Run created shim but print <target_executable> instead of executing it.");
            Console.WriteLine();
            Console.WriteLine(" --mkshim-test");
            Console.WriteLine("   Tests if shim's <target_executable> exists.");
            return true;
        }

        return false;
    }
}