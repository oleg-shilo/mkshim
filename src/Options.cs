//css_ref IconExtractor.dll
using System;
using System.ComponentModel;
using System.IO;
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