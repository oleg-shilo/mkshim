//css_ref IconExtractor.dll
using System;
using System.ComponentModel;

class RunOptions
{
    public string ShimName;
    public string TargetExecutable;

    [CliArg("--help|-help|-h|-?|?")]
    public bool HelpRequest;

    [CliArg("--version|-v")]
    public bool VersionRequest;

    [CliArg("--params|-p")]
    public string DefaultArguments;

    [CliArg("--icon")]
    public string IconFile;

    [CliArg("--relative|-r")]
    public bool RelativeTargetPath;

    [CliArg("--elevate")]
    public bool ShimRequiresElevation;

    [CliArg("--no-overlay")]
    public bool NoOverlay;

    //---------------------------------------
    [CliArg("--console|-c")]
    public bool Console;

    [CliArg("--console-hidden|-ch")]
    public bool ConsoleHidden;

    [CliArg("--win|-w")]
    public bool Windows;

    [CliArg("--no-console|-nc")]
    [Obsolete("Use --win instead.")]
    public bool NoConsole;

    [CliArg("--wait-onexit")]
    [Obsolete("Use --wait-pause instead.")]
    public bool WaitBeforeExit;

    [CliArg("--wait-pause")]
    public bool WaitPause;

    public bool IsRunable => !HelpRequest && !VersionRequest;
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