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
            // `Run` needs to be a separate method so premature assembly loading is avoided before we set up the assembly probing
            SetupAssemblyProbing();
            Run(args);
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

    static void Run(string[] args)
    {
        RunOptions options = args.Parse().Process().Validate();
        RunOptions rawOptions = options.Clone();

        if (options.Patch == true || options.PatchRemove == true)
        {
            options.Patch = false; // to avoid recursion

            if (!File.Exists(options.ShimName))
            {
                Console.WriteLine($"Cannot find the shim to patch at: {options.ShimName}");
                return;
            }

            var buildCommand = options.ShimName.ExtractBuildCommandOfShim();
            var originalOptions = new RunOptions().InitFrom(buildCommand);

            if (options.PatchRemove == true)
                options = originalOptions.Remove(options).Validate();
            else
                options = originalOptions.MergeWith(options).Validate();
        }

        if (HandleNonRunableInput(options))
            return;

        var buildDir = Path.Combine(Path.GetTempPath(), $"mkshim-{Guid.NewGuid()}");
        try
        {
            Directory.CreateDirectory(buildDir);

            (bool isWinApp, bool is64App) = options.TargetExecutable.GetPeInfo();

            // check if the app selection was forced by the user input
            if (options.Windows == true)
                isWinApp = true;
            else if (options.Console == true || options.ConsoleHidden == true)
                isWinApp = false;

            var icon =
                options.IconFile ??
                options.TargetExecutable.LookupPackageIcon() ??
                options.TargetExecutable.ExtractFirstIconToFolder(buildDir, handeErrors: true) ??
                options.TargetExecutable.ExtractDefaultAppIconToFolder(buildDir);

            if (icon?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
            {
                icon = icon.ExtractFirstIconToFolder(buildDir);
            }

            if (options.NoOverlay != true)
                icon = icon?.ApplyOverlayToIcon(icon.ChangeDir(buildDir));

            var targetRuntimePath = options.TargetExecutable;
            if (options.RelativeTargetPath == true)
                targetRuntimePath = options.TargetExecutable.ToRelativePathFrom(options.ShimName.GetDirName());

            var buildCommand = (rawOptions.Patch == true || rawOptions.PatchRemove == true) ?
                options.ComposeCommandLine() :
                Environment.CommandLine;

            var csFile = options.TargetExecutable.GenerateShimSourceCode(buildDir, isWinApp, options.DefaultArguments, targetRuntimePath, options.WaitPause == true, options.ConsoleHidden == true, buildCommand);

            var manifestFile = options.ShimRequiresElevation?.GenerateShimManifestFile(buildDir);

            var res = options.TargetExecutable.GenerateResFor(buildDir, options.DefaultArguments, icon, manifestFile, targetRuntimePath);

            if (res == null)
            {
                Console.WriteLine($"WARNING: Cannot generate shim resources with rc.exe. The shim file will have no MkShim related properties.");
                Console.WriteLine(compileLog);
                Console.WriteLine("---");
            }

            var appRes = (res != null ? $"/win32res:\"{res}\"" : "");
            var cpu = (is64App ? "/platform:x64" : "");

            var build = csc.RunCompiler($"-out:\"{options.ShimName}\" {appRes} {cpu} /target:{(isWinApp ? "winexe" : "exe")} \"{csFile}\"", compileLog);
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

    static string GenerateShimManifestFile(this bool requresElevation, string outDir)
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

    static string GenerateShimSourceCode(this string exe, string outDir, bool isWinApp, string defaultArgs, string exeRuntimePath, bool pauseBeforeExit, bool hiddenConsole, string buildCommand)
    {
        var version = exe.GetFileVersion().FileVersion;
        var template = Encoding.Default.GetString(Resource1.ConsoleShim);
        var buildCommandString = buildCommand
            .Replace(Assembly.GetExecutingAssembly().Location, "")
            .Replace("\"\"", "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Trim();

        var csFile = Path.Combine(outDir, Path.GetFileName(exe) + ".cs");

        var code = template.Replace("//{version}", $"[assembly: System.Reflection.AssemblyFileVersionAttribute(\"{version}\")]")
                           .Replace("//{target}", $"[assembly: System.Reflection.AssemblyDescriptionAttribute(@\"Shim to {exeRuntimePath}\")]")
                           .Replace("//{appFile}", $"static string appFile = @\"{exeRuntimePath}\";")
                           .Replace("//{isConsoleFile}", $"static bool isConsole = {(!isWinApp ? "true" : "false")};")
                           .Replace("//{defaultArgs}", $"static string defaultArgs = \"{defaultArgs} \";")
                           .Replace("//{waitForExit}", $"var toWait = {(isWinApp ? "false" : "true")};")
                           .Replace("//{hideConsole}", "HideConsoleWindowIfNotInTerminal();")
                           .Replace("//{buildCommand}", buildCommandString)
                           .Replace("//{setPause}", pauseBeforeExit ? "pause = true;" : "");

        File.WriteAllText(csFile, code);

        return csFile;
    }

    internal static string ThisAssemblyFileVersion => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

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
            VALUE ""CompanyName"", ""{targetFileMetadata.CompanyName}""
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
        var build = rc.RunCompiler(rcFile, compileLog);

        build.WaitForExit();

        return build.ExitCode == 0 ? resFile : null;
    }

    static StringBuilder compileLog = new StringBuilder();

    static bool HandleNonRunableInput(RunOptions options)
    {
        if (options.VersionRequest == true)
        {
            Console.WriteLine(ThisAssemblyFileVersion);
            return true;
        }

        if (options.HelpRequest == true)
        {
            Console.WriteLine(options.GenerateCliHelp());
            return true;
        }

        return false;
    }

    private static void SetupAssemblyProbing()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, arg) =>
                                arg.Name.Contains("System.Buffers") ? Assembly.Load(Resource1.System_Buffers) :
                                arg.Name.Contains("System.Collections.Immutable") ? Assembly.Load(Resource1.System_Collections_Immutable) :
                                arg.Name.Contains("System.Memory") ? Assembly.Load(Resource1.System_Memory) :
                                arg.Name.Contains("System.Numerics.Vectors") ? Assembly.Load(Resource1.System_Numerics_Vectors) :
                                arg.Name.Contains("System.Reflection.Metadata") ? Assembly.Load(Resource1.System_Reflection_Metadata) :
                                arg.Name.Contains("System.Runtime.CompilerServices.Unsafe") ? Assembly.Load(Resource1.System_Runtime_CompilerServices_Unsafe) :
                                null;
    }
}