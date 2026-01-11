//css_ref IconExtractor.dll
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

class RunOptions
{
    public string ShimName;
    public string TargetExecutable;

    [CliArg("--help|-help|-h|-?|?")]
    public bool? HelpRequest;

    [CliArg("--version|-v")]
    public bool? VersionRequest;

    [CliArg("--patch|-pt")]
    public bool? Patch;

    [CliArg("--patch-remove|-pt-rm")]
    public bool? PatchRemove;

    [CliArg("--params|-p")]
    public string DefaultArguments;

    [CliArg("--icon")]
    public string IconFile;

    [CliArg("--relative|-r")]
    public bool? RelativeTargetPath;

    [CliArg("--elevate")]
    public bool? ShimRequiresElevation;

    [CliArg("--no-overlay")]
    public bool? NoOverlay;

    //---------------------------------------
    [CliArg("--console|-c")]
    public bool? Console;

    [CliArg("--console-hidden|-ch")]
    public bool? ConsoleHidden;

    [CliArg("--win|-w")]
    public bool? Windows;

    [CliArg("--no-console|-nc")]
    [Obsolete("Use --win instead.")]
    public bool? NoConsole;

    [CliArg("--wait-onexit")]
    [Obsolete("Use --wait-pause instead.")]
    public bool? WaitBeforeExit;

    [CliArg("--wait-pause")]
    public bool? WaitPause;

    public bool IsRunable => HelpRequest != true && VersionRequest != true;

    public string ComposeCommandLine()
    {
        var result = new StringBuilder();

        result.Append($"\"{ShimName}\"");
        result.Append($" \"{TargetExecutable}\"");

        if (HelpRequest == true) result.Append(" --help");
        if (VersionRequest == true) result.Append(" --version");
        if (Patch == true) result.Append(" --patch");
        if (PatchRemove == true) result.Append(" --patch-remove");
        if (RelativeTargetPath == true) result.Append(" --relative");
        if (NoOverlay == true) result.Append(" --no-overlay");
        if (ShimRequiresElevation == true) result.Append(" --elevate");
        if (IconFile.HasText()) result.Append($"  \"--icon:{IconFile}\"");
        if (Console == true) result.Append(" --console");
        if (WaitPause == true || WaitBeforeExit == true) result.Append(" --wait-pause");
        if (DefaultArguments.HasText()) result.Append($"  \"--params:{DefaultArguments}\"");
        if (ConsoleHidden == true) result.Append(" --console-hidden");
        if (Windows == true || NoConsole == true) result.Append(" --win");

        return result.ToString().Trim();
    }

    public RunOptions Clone() => (RunOptions)this.MemberwiseClone();

    public RunOptions Process()
    {
        if (this.Patch == true || this.PatchRemove == true)
        {
            this.TargetExecutable = this.ShimName.ExtractTargetOfShim();
        }
        return this;
    }

    public RunOptions MergeWith(RunOptions extraOptions)
    {
        var result = this.Clone();
        var defaultValues = new RunOptions();

        foreach (var field in typeof(RunOptions).GetFields())
        {
            var extraValue = field.GetValue(extraOptions);
            var defaultValue = field.GetValue(defaultValues);

            if (extraValue != null && !extraValue.Equals(defaultValue))
            {
                field.SetValue(result, extraValue);
            }
        }
        return result;
    }

    public RunOptions Remove(RunOptions extraOptions)
    {
        var result = this.Clone();
        var defaultValues = new RunOptions();
        foreach (var field in typeof(RunOptions).GetFields())
        {
            if (field.Name == nameof(this.TargetExecutable) || field.Name == nameof(this.ShimName))
                continue;

            var extraValue = field.GetValue(extraOptions);
            var defaultValue = field.GetValue(defaultValues);
            if (extraValue != null && !extraValue.Equals(defaultValue))
            {
                field.SetValue(result, defaultValue);
            }
        }
        return result;
    }

    public RunOptions InitFrom(string cmdLine)
    {
        var args = cmdLine.ParseCliArgs();
        return InitFrom(args);
    }

    public RunOptions InitFrom(string[] args)
    {
        this.ShimName = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[0])).EnsureExtension(".exe");

        this.IconFile = args.GetValueFor(nameof(this.IconFile));
        this.NoOverlay = args.HaveArgFor(nameof(this.NoOverlay));
        this.ShimRequiresElevation = args.HaveArgFor(nameof(this.ShimRequiresElevation));
        this.RelativeTargetPath = args.HaveArgFor(nameof(this.RelativeTargetPath));
        this.DefaultArguments = args.GetValueFor(nameof(this.DefaultArguments))?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        this.WaitPause = args.HaveArgFor(nameof(this.WaitPause));
        this.NoConsole = args.HaveArgFor(nameof(this.NoConsole));
        this.Windows = args.HaveArgFor(nameof(this.Windows));
        this.Console = args.HaveArgFor(nameof(this.Console));
        this.ConsoleHidden = args.HaveArgFor(nameof(this.ConsoleHidden));
        this.Patch = args.HaveArgFor(nameof(this.Patch));
        this.PatchRemove = args.HaveArgFor(nameof(this.PatchRemove));

        if (this.Patch != true && this.PatchRemove != true)
            this.TargetExecutable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1])).EnsureExtension(".exe");
        return this;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class CliArgAttribute : Attribute
{
    public CliArgAttribute(string name)
    {
        this.Name = name;
    }

    public string Name { get; set; }
}

static class RunOptionsExtension
{
    public static string GenerateCliHelp(this RunOptions options)
    {
        string thisAssemblyFileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

        return new StringBuilder()
        .AppendLine($"{thisAssemblyFileVersion}) - Shim generator")
        .AppendLine("Copyright(C) 2024 - 2026 Oleg Shilo (github.com/oleg-shilo)")
        .AppendLine($@"Generates shim for a given executable file.")
        .AppendLine()
        .AppendLine($@"Usage:")
        .AppendLine($@"   mkshim <shim_name> <target_executable> [options]")
        .AppendLine()
        .AppendLine("shim_name")
        .AppendLine("    Path to the shim to be created.")
        .AppendLine("    The `.exe` extension will be assumed if the file path was specified without an extension.")
        .AppendLine()
        .AppendLine("target_executable")
        .AppendLine("    Path to the target executable to be pointed to by the created shim.")
        .AppendLine("    The `.exe` extension will be assumed if the file path was specified without an extension.")
        .AppendLine()
        .AppendLine("Options:")
        .AppendLine()
        .AppendLine(nameof(options.VersionRequest).GetCliName())
        .AppendLine("    Prints MkShim version.")
        .AppendLine()
        .AppendLine("--params:<args> | -p:<args>")
        .AppendLine("    The default arguments you always want to pass to the target executable.")
        .AppendLine("    IE with chrome.exe shim: 'chrome.exe --save-page-as-mhtml --user-data-dir=\"/some/path\"'")
        .AppendLine()
        .AppendLine("--icon:<iconfile>")
        .AppendLine("    The custom icon (or exe with the app icon) to be embedded in the shim. If not specified then the icon will be resolved in the following order:")
        .AppendLine("    1. The application package icon will be looked up in the current and parent folder.")
        .AppendLine("       The expected package icon name is `favicon.ico` or  `<app>.ico`.")
        .AppendLine("    2. The icon of the target file.")
        .AppendLine("    3. MkShim application icon.")
        .AppendLine()
        .AppendLine(nameof(options.RelativeTargetPath).GetCliName())
        .AppendLine("    The created shim is to point to the target executable by the relative path with respect to the shim location.")
        .AppendLine("    Note, if the shim and the target path are pointing to the different drives the resulting path will be the absolute path to the target.")
        .AppendLine()
        .AppendLine(nameof(options.NoConsole).GetCliName())
        .AppendLine("    No console option.")
        .AppendLine("    MkShim decided what time of shim to build (console vs window) based on the target executable type. Basically it is matching the target exe type.")
        .AppendLine("    However if your target exe is a console and for whatever reason you want to build a widow shim then you can use this option.")
        .AppendLine()
        .AppendLine(nameof(options.NoOverlay).GetCliName())
        .AppendLine("    Disable embedding 'shim' overlay to the application icon of the shim executable.")
        .AppendLine("    By default MkShim always creates an overlay to visually distinguish the shim from the target file.")
        .AppendLine()
        .AppendLine(nameof(options.WaitPause).GetCliName())
        .AppendLine("    Build shim that waits for user input before exiting.")
        .AppendLine("    It is an equivalent of the command `pause` in batch file.")
        .AppendLine()
        .AppendLine(nameof(options.ShimRequiresElevation).GetCliName())
        .AppendLine("    Build the shim that requires elevation at startup.")
        .AppendLine("    By default MkShim creates the shim that does not require elevation")
        .AppendLine()
        .AppendLine(nameof(options.Windows).GetCliName())
        .AppendLine("    Forces the shim application to be a window (GUI) application regardless the target application type.")
        .AppendLine("    A window application has no console window attached to the process. Like Windows Notepad application.")
        .AppendLine("    Note, such application will return immediately if it is executed from the batch file or console.")
        .AppendLine("    See https://github.com/oleg-shilo/mkshim/wiki#use-cases")
        .AppendLine()
        .AppendLine(nameof(options.Console).GetCliName())
        .AppendLine("    Forces the shim application to be a console application regardless the target application type.")
        .AppendLine("    Note, such application will not return if it is executed from the batch file or console until the target application exits.")
        .AppendLine("    See https://github.com/oleg-shilo/mkshim/wiki#use-cases")
        .AppendLine()
        .AppendLine(nameof(options.ConsoleHidden).GetCliName())
        .AppendLine("    This switch is a full equivalent of `--console` switch. But during the execution it hides.")
        .AppendLine("    Note, such application will not return if it is executed from the batch file or console until the target application exits.")
        .AppendLine("    See https://github.com/oleg-shilo/mkshim/wiki#use-cases")
        .AppendLine()
        .AppendLine(nameof(options.Patch).GetCliName())
        .AppendLine("    Patches the existing shim by rebuilding it with the original build command but with the individual options parameters substituted with the user specified input.")
        .AppendLine("    Example:")
        .AppendLine("       Original command:  `mkshim app application.exe \"-p:param1 param2\" --win`")
        .AppendLine("       Patch command:     `mkshim app \"-p:param3\"` --elevate --patch")
        .AppendLine("       New build command: `mkshim app application.exe \"-p:param3\"` --win --elevate")
        .AppendLine("    Note, using this option may lead to unpredictable results if the shim was build with using relative paths.")
        .AppendLine()
        .AppendLine(nameof(options.PatchRemove).GetCliName())
        .AppendLine("    Patches the existing shim by rebuilding it with the original build command but with the individual options parameters removed if they are present in the user specified input.")
        .AppendLine("    Example:")
        .AppendLine("       Original command: `mkshim app application.exe --elevate --win`")
        .AppendLine("       Patch command:    `mkshim app --elevate --patch-remove")
        .AppendLine("       New shim command: `mkshim app application.exe --win")
        .AppendLine("    Note, using this option may lead to unpredictable results if the shim was build with using relative paths.")
        .AppendLine()
        .AppendLine(nameof(options.HelpRequest).GetCliName())
        .AppendLine("    Prints this help content.")
        .AppendLine()
        .AppendLine("Runtime:")
        .AppendLine()
        .AppendLine("The shim always runs the target executable in a separate process")
        .AppendLine("You can use special MkShim arguments with the created shim:")
        .AppendLine(" --mkshim-noop")
        .AppendLine("   RunCompiler created shim but print <target_executable> instead of executing it. It also prints the details of the shim.")
        .AppendLine()
        .AppendLine(" --mkshim-test")
        .AppendLine("   Tests if shim's <target_executable> exists.")
        .ToString();
    }

    public static RunOptions Parse(this string[] args)
    {
        args.ValidateCliArgs();

        var options = new RunOptions();

        if (args.HaveArgFor(nameof(options.HelpRequest)) == true)
        {
            options.HelpRequest = true;
        }
        else if (args.HaveArgFor(nameof(options.VersionRequest)) == true)
        {
            options.VersionRequest = true;
        }
        else if (args.Length < 2)
        {
            throw new ValidationException("Not enough arguments were specified. Execute 'mkshim -?' for usage help.");
        }
        else
        {
            options.InitFrom(args);
        }
        return options;
    }

    public static RunOptions Validate(this RunOptions options)
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
}

static class CliArgExtension
{
    public static string GetCliName(this string argumentName)
    {
        var optionsField = typeof(RunOptions).GetFields().FirstOrDefault(x => x.Name == argumentName);
        var cliName = optionsField?.GetCustomAttributes(typeof(CliArgAttribute), true).Cast<CliArgAttribute>()
                                   .FirstOrDefault()?
                                   .Name;

        // "--relative|-r"
        return cliName?.Replace("|", " | "); ;
    }
}