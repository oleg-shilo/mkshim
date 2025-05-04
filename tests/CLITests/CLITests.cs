using System.Diagnostics;

namespace mkshim.tests
{
    public class CLITests
    {
        string mkshim_ico => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "logo.ico"));
        string mkshim_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "distro", "mkshim.exe"));
        string mkshim_version => typeof(CLITests).Assembly.GetName().Version.ToString();
        string target_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestTargetApp", "targetapp.exe"));
        string target_version => "1.1.1.0";

        //------------------------------------------------------

        [Fact]
        public void InvalidCliCommand()
        {
            var expectedLine = "Not enough arguments were specified";

            var output = mkshim_exe.Run();
            Assert.Contains(expectedLine, output);
        }

        [Fact]
        public void PrintHelp()
        {
            var expectedLine = "Oleg Shilo (github.com/oleg-shilo)";

            var output = mkshim_exe.Run("--help");
            Assert.Contains(expectedLine, output);

            output = mkshim_exe.Run("-help");
            Assert.Contains(expectedLine, output);

            output = mkshim_exe.Run("-h");
            Assert.Contains(expectedLine, output);

            output = mkshim_exe.Run("-?");
            Assert.Contains(expectedLine, output);

            output = mkshim_exe.Run("?");
            Assert.Contains(expectedLine, output);
        }

        [Fact]
        public void HelpCoverage()
        {
            var exectedSwitches = TestExtensions.GetCliSwitches();

            var output = mkshim_exe.Run("--help");

            var reportedSwitches = output
                .Split("\n--").Skip(1)
                .ToDictionary(x => "--" + string.Join('|', x.GetLines().First().Replace(" ", "").Split('|').Select(x => x.Split(':').First())), //--params|-p vs --params:<args> | -p:<args>
                              y => y.GetLines().Skip(1).Where(x => x.StartsWith("  ") && !string.IsNullOrEmpty(x)).ToArray());

            var missingInfo = exectedSwitches.Except(reportedSwitches.Keys).ToArray();

            Assert.True(
                missingInfo.Length == 0,
                $"Some CLI switches are not implemented: " + string.Join(", ", missingInfo));
        }

        [Fact]
        public void PrintVersion()
        {
            var retortedVersion = mkshim_exe.Run("--version");
            Assert.Equal(mkshim_version, retortedVersion);

            retortedVersion = mkshim_exe.Run("-v").Trim();
            Assert.Equal(mkshim_version, retortedVersion);
        }

        [Fact]
        public void BasicScenario()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");

            Assert.Contains("The shim has been created", output);
            _Assert.FileExists(shim_exe);
        }

        [Fact]
        public void BasicScenarioError()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"f:\\invalid-dir\\target.exe\"");
            Assert.Contains("Target executable cannot be found at:", output);

            output = mkshim_exe.Run($"\"f:\\invalid-dir\\shim.exe\" \"{target_exe}\" ");
            Assert.Contains("Shim parent directory does not exist:", output);
        }

        [Fact]
        public void RelativeTargetPath()
        {
            // setup
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim1_exe = dir.Combine("shim1.exe");
            var relativeTargetPath = Path.GetRelativePath(Path.GetDirectoryName(shim_exe), target_exe);

            // test
            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" -r");
            var output1 = mkshim_exe.Run($"\"{shim1_exe}\" \"{target_exe}\" --relative");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim1_exe);

            output = shim_exe.Run($"--mkshim-noop");
            output1 = shim1_exe.Run($"--mkshim-noop");

            Assert.Equal(output, output1);

            var actualTargetPath = output.GetLines().First(x => x.Contains("Target: ")).Replace("Target:", "").Trim();

            Assert.Contains(relativeTargetPath, actualTargetPath);
        }

        [Fact]
        public void NoConsoleShim()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim_nc_exe = dir.Combine("shimNoConsole.exe");
            var shim_nc2_exe = dir.Combine("shimNoConsole2.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            mkshim_exe.Run($"\"{shim_nc_exe}\" \"{target_exe}\" -nc");
            mkshim_exe.Run($"\"{shim_nc2_exe}\" \"{target_exe}\" --no-console");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim_nc_exe);
            _Assert.FileExists(shim_nc2_exe);

            Assert.True(shim_exe.IsConsoleApp());
            Assert.False(shim_nc_exe.IsConsoleApp());
            Assert.False(shim_nc2_exe.IsConsoleApp());
        }

        [Theory]
        [InlineData("--console"), InlineData("-c"), InlineData("--console-hidden"), InlineData("-ch")]
        public void Forced_ShimConsoleType_Selection(string @switch)
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe {@switch}");

            _Assert.FileExists(shim_exe);

            Assert.True(shim_exe.IsConsoleApp());
        }

        [Theory]
        [InlineData("--win"), InlineData("-w")]
        public void Forced_ShimWinType_Selection(string @switch)
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" {@switch}");

            _Assert.FileExists(shim_exe);

            Assert.False(shim_exe.IsConsoleApp());
        }

        [Fact]
        public void AutoShimAppTypeSelection()
        {
            var dir = this.PrepareDir();
            var shim_console_exe = dir.Combine("shim.console.exe");
            var shim_win_exe = dir.Combine("shim.win.exe");

            mkshim_exe.Run($"\"{shim_console_exe}\" \"{target_exe}\"");
            mkshim_exe.Run($"\"{shim_win_exe}\" C:\\Windows\\notepad.exe");

            _Assert.FileExists(shim_console_exe);
            _Assert.FileExists(shim_win_exe);

            Assert.True(shim_console_exe.IsConsoleApp());
            Assert.False(shim_win_exe.IsConsoleApp());
        }

        [Fact]
        public void WaitForTargetApp()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            _Assert.FileExists(shim_exe);

            var timestamp = Environment.TickCount;
            shim_exe.Run("-wait-for-5000");            // app will wait for 5 seconds.
            var executionTime = Environment.TickCount - timestamp;

            Assert.True(executionTime >= 5000);
        }

        [Fact]
        public void PauseBeforeExitShim()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim_wait_exe = dir.Combine("shimWaitConsole.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            mkshim_exe.Run($"\"{shim_wait_exe}\" \"{target_exe}\" --wait-pause");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim_wait_exe);

            // no wait app
            var executionTime = Profiler.Measure(() => shim_exe.Run());

            Assert.True(executionTime < 500); // less than 0.5 second

            // app with waiting for any key
            executionTime = Profiler.Measure(() =>
                shim_wait_exe.RunWithDelayedInput("", "x", 5000));

            Assert.True(executionTime > 500); // more than 0.5 second
        }

        [Fact]
        public void InjectShimResources()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            _Assert.FileExists(shim_exe);

            output = "sigcheck".Run($"\"{shim_exe}\"");

            Assert.Contains($"(created with mkshim v{mkshim_version})", output);
            Assert.Contains($"Product:\t{target_exe}", output);
            Assert.Contains($"Prod version:\t{target_version}", output);
            Assert.Contains($"File version:\t{target_version}", output);
            Assert.Contains($"Company:\tMkShim Company", output);
        }

        [Fact]
        public void runtime_Mkshim_NoOp()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" \"-p:param1 param2\"");

            _Assert.FileExists(shim_exe);

            output = shim_exe.Run($"--mkshim-noop");
            Assert.Contains($"Target: {target_exe}", output);
            Assert.Contains($"Default params: param1 param2", output);
        }

        [Fact]
        public void runtime_Mkshim_Test()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            _Assert.FileExists(shim_exe);

            output = shim_exe.Run($"--mkshim-test");
            Assert.Contains($"Success: target file exists.", output);
            Assert.Contains($"Target: {target_exe}", output);
        }

        [Fact]
        public void runtime_Mkshim_DoNotWait_ForTarget()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim_exe_log = dir.Combine("shim.exe.log");

            if (File.Exists(shim_exe_log))
                File.Delete(shim_exe_log);

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            _Assert.FileExists(shim_exe);

            var shimExecutionTime = Profiler.Measure(() =>
                output = shim_exe.Run($"--mkshim-exit -wait-for-5000 -log-events-to \"{shim_exe_log}\"", ignoreOutput: true));

            Thread.Sleep(6000); // give the target app time to start and exit

            _Assert.FileExists(shim_exe_log);
            var log = File.ReadAllLines(shim_exe_log)
                .Where(x => x.Contains(": target started") || x.Contains(": target exited"))
                .Select(x => x.Replace(": target started", "").Replace(": target exited", ""));

            Assert.Equal(2, log.Count());

            var targetExecutionTime = (DateTime.Parse(log.Last()) - DateTime.Parse(log.First())).TotalMilliseconds;

            Assert.True(shimExecutionTime < 500); // less than 0.5 second
            Assert.True(targetExecutionTime > 4500); // 5 seconds or more
        }

        [Fact]
        public void DefaultParameters()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output1 = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" \"-p:param1 param2\"");
            _Assert.FileExists(shim_exe);

            var output2 = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" \"--params:param1 param2\"");
            _Assert.FileExists(shim_exe);
            Assert.Equal(output1, output2);

            var allParameters = shim_exe.Run($"param3").GetLines();

            Assert.True(allParameters.Count() == 3);
            Assert.Equal("param1", allParameters[0]);
            Assert.Equal("param2", allParameters[1]);
            Assert.Equal("param3", allParameters[2]);
        }

        [Fact]
        public void manual_HiddenConsoleShim_Test()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe ");
            _Assert.FileExists(shim_exe);

            Assert.Fail("Check manually that the shim is hidden");

            // Uncomment the next line and check visually if the overlay is present on the shim icon
            // shim_exe.Run();
        }

        [Fact]
        public void manual_OverlayTest()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim_nooverlay_exe = dir.Combine("shim-no.exe");

            mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe");
            mkshim_exe.Run($"\"{shim_nooverlay_exe}\" C:\\Windows\\notepad.exe --no-overlay");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim_nooverlay_exe);

            Assert.Fail("Check manually that the shim has overlay icon and the nooverlay shim does not have it");
            // Check visually if the overlay
            // - is present on shim_exe
            // - is not present on shim_nooverlay_exe
        }

        [Fact]
        public void manual_ElevatedShimTest()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" --elevate");
            _Assert.FileExists(shim_exe);

            Assert.Fail("Check manually that the shim prompts for elevation");

            // check manually that the shim prompts for elevation
        }

        [Fact]
        public void manual_CustomIconShimTest()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var output = mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe \"--icon:{mkshim_ico}\"");
            _Assert.FileExists(shim_exe);

            Assert.Fail("Check manually that the shim has mkshim's icon but not the notepad's one");
            // check manually that the shim has mkshim's icon but not the notepad's one
        }
    }
}