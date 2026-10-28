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
    }
}