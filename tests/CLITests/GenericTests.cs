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

            Assert.Equal(target2_exe, options.TargetExecutable);
            Assert.Equal(2, options.AllTargetExecutables.Count);
            Assert.Contains(target_exe, options.AllTargetExecutables);
            Assert.Contains(target2_exe, options.AllTargetExecutables);
        }

        [Fact]
        public void MultipleTargets_NoImpact_on_existing_CLI()
        {
            RunOptions options;
            string[] args;

            args = ["shim.exe", target_exe];
            options = args.Parse().Process().Validate();
            Assert.Equal(target_exe, options.TargetExecutable);

            args = ["shim.exe", $"--target:{target_exe}"];
            options = args.Parse().Process().Validate();
            Assert.Equal(target_exe, options.TargetExecutable);
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
                AllTargetExecutables = ["target.exe", "target2.exe"]
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