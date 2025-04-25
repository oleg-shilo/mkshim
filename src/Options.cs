//css_ref IconExtractor.dll
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

    [CliArg("--no-console|-nc")]
    public bool NoConsole;

    [CliArg("--no-overlay")]
    public bool NoOverlay;

    [CliArg("--wait-onexit")]
    public bool WaitBeforeExit;

    [CliArg("--elevate")]
    public bool ShimRequiresElevation;

    public bool IsRunable => !HelpRequest && !VersionRequest;
}