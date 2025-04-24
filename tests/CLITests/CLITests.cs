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
            throw new NotImplementedException("Relative target path test not implemented yet.");
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