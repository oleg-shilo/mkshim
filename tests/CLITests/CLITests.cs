using System.Diagnostics;
using System.Drawing;
using TsudaKageyu;

namespace mkshim.tests
{
    public class CLITests : CLITestBase
    {
        [Fact]
        public void Invalid_CliCommand()
        {
            var expectedLine = "Not enough arguments were specified";

            var output = mkshim_exe.Run();
            Assert.Contains(expectedLine, output);
        }

        [Fact]
        public void Print_Help()
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
        public void Help_Coverage()
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
        public void Print_Version()
        {
            var retortedVersion = mkshim_exe.Run("--version");
            Assert.Equal(mkshim_version, retortedVersion);

            retortedVersion = mkshim_exe.Run("-v").Trim();
            Assert.Equal(mkshim_version, retortedVersion);
        }

        [Fact]
        public void Report_Obsolete_CliArgs()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" --wait-onexit");

            Assert.Contains("\"Use --wait-pause instead.\"", output);
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
        public void BasicScenario_Error()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"f:\\invalid-dir\\target.exe\"");
            Assert.Contains("Target executable cannot be found at:", output);

            output = mkshim_exe.Run($"\"f:\\invalid-dir\\shim.exe\" \"{target_exe}\" ");
            Assert.Contains("Shim parent directory does not exist:", output);
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
        public void Shim_Relative_TargetPath()
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

            // first 3 liknes can be different (e.g. 'Build shim command:')
            output = string.Join("\n", shim_exe.Run($"--mkshim-noop").GetLines().Skip(3));
            output1 = string.Join("\n", shim1_exe.Run($"--mkshim-noop").GetLines().Skip(3));

            Assert.Equal(output, output1);

            var actualTargetPath = output.GetLines().First(x => x.Contains("Target: ")).Replace("Target:", "").Trim();

            Assert.Contains(relativeTargetPath, actualTargetPath);
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
        public void ShimWinType_Forced_Selection(string @switch)
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" {@switch}");

            _Assert.FileExists(shim_exe);

            Assert.False(shim_exe.IsConsoleApp());
        }

        [Fact]
        public void ShimAppType_Auto_Selection()
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
        public void Wait_For_TargetApp()
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
        public void Pause_BeforeExit_Shim()
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
        public void Inject_ShimResources()
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
        public void AppIcon_Overlay()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim-overlay.exe");
            var shim_nooverlay_exe = dir.Combine("shim-no-overlay.exe");
            var overlay_file = dir.Combine("overlay.png");

            mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe");
            mkshim_exe.Run($"\"{shim_nooverlay_exe}\" C:\\Windows\\notepad.exe --no-overlay");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim_nooverlay_exe);

            // it will extract first bitmap of the first icon that is a 32x32 bitmap
            var bitmap_file = shim_exe.GetFirstIconBitmap();
            var bitmap_nooverlay_file = shim_nooverlay_exe.GetFirstIconBitmap();

            var testManually = false;

            if (!testManually)
            {
                mkshim_overlay_32.CropLeftBottomOverlayAndSaveTo(dir.Combine("overlay.png"));

                bool found = overlay_file.IsInImageQuadrant(bitmap_file, SearchQuadrant.BottomLeft);
                Assert.True(found);

                found = overlay_file.IsInImageQuadrant(bitmap_nooverlay_file, SearchQuadrant.BottomLeft);
                Assert.False(found);
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = bitmap_file, UseShellExecute = true });           // that the overlay is present on shim_with_custom_icon_exe
                Process.Start(new ProcessStartInfo { FileName = bitmap_nooverlay_file, UseShellExecute = true }); // that the overlay is not present on shim_nooverlay_exe
            }
        }

        [Fact]
        public void TargetIcon_Shim()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"  \"--icon:{mkshim_ico}\" --no-overlay");

            _Assert.FileExists(shim_exe);

            var custom_bitmap = mkshim_ico.GetIconFirstBitmap(dir.Combine("expected.png"));
            var bitmap_from_shim = shim_exe.GetFirstIconBitmap();

            _Assert.FilesAreSame(custom_bitmap, bitmap_from_shim);
        }

        [Fact]
        public void CustomIcon_Shim()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"  --no-overlay");

            _Assert.FileExists(shim_exe);

            var bitmap_from_target = target_exe.GetFirstIconBitmap();
            var bitmap_from_shim = shim_exe.GetFirstIconBitmap();

            _Assert.FilesAreSame(bitmap_from_target, bitmap_from_shim);
        }

        [Fact]
        public void runtime_Mkshim_NoOp()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var command = $"\"{shim_exe}\" \"{target_exe}\" \"-p:param1 param2\"";

            var output = mkshim_exe.Run(command);

            _Assert.FileExists(shim_exe);

            output = shim_exe.Run($"--mkshim-noop");

            Assert.Contains($"Target: {target_exe}", output);
            Assert.Contains($"Default params: param1 param2", output);
            Assert.Contains($"Build shim command: mkshim " + $"\"{shim_exe}\" \"{target_exe}\" \"-p:param1 param2\"", output);
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

        // [Fact]
        [Fact(Skip = "Manual test. Uncomment `\"explorer\".Run(...)` and test the shim for hidden console...")]
        public void manual_HiddenConsoleShim_Test()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" C:\\Windows\\notepad.exe --console-hidden");
            _Assert.FileExists(shim_exe);
            Assert.True(shim_exe.IsConsoleApp());

            // "explorer".Run(dir);
        }

        // [Fact]
        [Fact(Skip = "Manual test. Uncomment `\"explorer\".Run(...)` and check visually if the shim prompts for elevation.")]
        public void manual_ElevatedShimTest()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\" --elevate");
            _Assert.FileExists(shim_exe);

            // "explorer".Run(dir);
        }
    }
}