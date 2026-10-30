// Ignore Spelling: CLI

using System.Diagnostics;
using System.Drawing;
using TsudaKageyu;

namespace mkshim.tests
{
    public class GenericTests : CLITestBase
    {
        [Fact]
        public void MultipleTargets()
        {
            RunOptions options;
            string[] args;

            args = ["shim.exe", $"--target:{target2_exe}", $"--target:{target_exe}"];
            options = args.Parse().Process().Validate();

            Assert.Equal(target2_exe, options.FirstAvailableTarget.Path);
            Assert.Equal(2, options.AllTargets.Count);

            Assert.Equal(target2_exe, options.AllTargets[0].Path);
            Assert.Equal(target_exe, options.AllTargets[1].Path);
        }

        [Fact]
        public void MultipleTargets_with_condition()
        {
            RunOptions options;
            string[] args;

            args = ["shim.exe", $"--target:{target2_exe}", $"--target:{target_exe}?elevated"];
            options = args.Parse().Process().Validate();

            Assert.Equal(target2_exe, options.FirstAvailableTarget.Path);
            Assert.Equal(2, options.AllTargets.Count);

            Assert.Equal(target2_exe, options.AllTargets[0].Path);
            Assert.False(options.AllTargets[0].OnlyWhenElevated);

            Assert.Equal(target_exe, options.AllTargets[1].Path);
            Assert.True(options.AllTargets[1].OnlyWhenElevated);
        }

        [Fact]
        public void MultipleTargets_NoImpact_on_existing_CLI()
        {
            RunOptions options;
            string[] args;

            args = ["shim.exe", target_exe];
            options = args.Parse().Process().Validate();
            Assert.Equal(target_exe, options.FirstAvailableTarget.Path);

            args = ["shim.exe", $"--target:{target_exe}"];
            options = args.Parse().Process().Validate();
            Assert.Equal(target_exe, options.FirstAvailableTarget.Path);
        }

        [Fact]
        public void RunOptions_MultiTargetCommandLine_RoundTrip()
        {
            var options = new RunOptions
            {
                DefaultArguments = "param1 param2",
                IconFile = "icon.ico",
                RelativeTargetPath = true,
                ShimRequiresElevation = true,
                NoOverlay = true,
                Console = true,
                ConsoleHidden = true,
                Windows = true,
                WaitPause = true,
                AllTargets = new List<TargetInfo> { new TargetInfo("target.exe", "target.exe"), new TargetInfo("target2.exe", "target2.exe") }
            };

            var cmdLine = options.ComposeCommandLine();

            var reconstructedOptions = new RunOptions().InitFrom(cmdLine);

            Assert.Equal(options.DefaultArguments, reconstructedOptions.DefaultArguments);
            Assert.Equal(options.IconFile, reconstructedOptions.IconFile);
            Assert.Equal(options.RelativeTargetPath, reconstructedOptions.RelativeTargetPath);
            Assert.Equal(options.ShimRequiresElevation, reconstructedOptions.ShimRequiresElevation);
            Assert.Equal(options.NoOverlay, reconstructedOptions.NoOverlay);
            Assert.Equal(options.Console, reconstructedOptions.Console);
            Assert.Equal(options.ConsoleHidden, reconstructedOptions.ConsoleHidden);
            Assert.Equal(options.Windows, reconstructedOptions.Windows);
            Assert.Equal(options.WaitPause, reconstructedOptions.WaitPause);
        }
    }
}