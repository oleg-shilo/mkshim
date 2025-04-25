using System.Diagnostics;

namespace mkshim.tests
{
    public class CLITests
    {
        string mkshim_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "distro", "mkshim.exe"));
        string mkshim_version => typeof(CLITests).Assembly.GetName().Version.ToString();
        string target_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestTargetApp", "targetapp.exe"));
        string target_version => "1.1.1.0";

        //------------------------------------------------------

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

        [Fact]
        public void WaitExecShim()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");
            var shim_wait_exe = dir.Combine("shimWaitConsole.exe");

            mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            mkshim_exe.Run($"\"{shim_wait_exe}\" \"{target_exe}\" --wait-onexit");

            _Assert.FileExists(shim_exe);
            _Assert.FileExists(shim_wait_exe);

            // no wait app

            var timestamp = Environment.TickCount;
            shim_exe.Run();
            var executionTime = Environment.TickCount - timestamp;

            Assert.True(executionTime < 500); // less than 0.5 second

            // app with waiting fro any key

            timestamp = Environment.TickCount;
            shim_wait_exe.RunWithDelayedInput("", "x", 5000);
            executionTime = Environment.TickCount - timestamp;

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
        public void runtime_MkshimNoOp()
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
        public void runtime_MkshimTest()
        {
            var dir = this.PrepareDir();
            var shim_exe = dir.Combine("shim.exe");

            var output = mkshim_exe.Run($"\"{shim_exe}\" \"{target_exe}\"");
            _Assert.FileExists(shim_exe);

            output = shim_exe.Run($"--mkshim-test");
            Assert.Contains($"Success: target file exists.", output);
            Assert.Contains($"Target: {target_exe}", output);
        }
    }
}