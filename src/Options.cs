//css_ref IconExtractor.dll
class RunOptions
{
    public string ShimName;
    public bool HelpRequest;
    public bool VersionRequest;
    public string TargetExecutable;
    public string DefaultArguments;
    public string IconFile;
    public bool NoOverlay;
    public bool WaitBeforeExit;
    public bool ShimRequiresElevation;
    public bool NoConsole;
    public bool RelativeTargetPath;
    public bool IsRunable => !HelpRequest && !VersionRequest;
}