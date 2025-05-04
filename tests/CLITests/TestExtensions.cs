using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace mkshim.tests
{
    static class Profiler
    {
        public static int Measure(Action action)
        {
            var timestamp = Environment.TickCount;
            action();
            return Environment.TickCount - timestamp;
        }
    }

    static class _Assert
    {
        public static void FileExists(this string path, [CallerMemberName] string caller = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = -1)
        {
            Assert.True(File.Exists(path), $"Assert.FileExists failure: {caller}\n{file} (line {line})");
        }
    }

    static class TestExtensions
    {
        public static string[] GetLines(this string text) => text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        public static string Combine(this string path, string path2) => Path.Combine(path, path2);

        public static string PrepareDir(this object test, [CallerMemberNameAttribute] string caller = null)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "test-output", caller);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static bool IsConsoleApp(this string exe)
        {
            using (var stream = File.OpenRead(exe))
            using (var peFile = new PEReader(stream))
                return peFile.PEHeaders.IsConsoleApplication;
        }

        public static string[] GetCliSwitches()
        {
            var switches = typeof(RunOptions).GetFields()
                .Where(x =>
                {
                    return x.GetCustomAttributes(typeof(ObsoleteAttribute), true).Count() == 0
                        && x.GetCustomAttributes(typeof(CliArgAttribute), true).Count() > 0;
                })
                .SelectMany(x => x.GetCustomAttributes(typeof(CliArgAttribute), true))
                .Cast<CliArgAttribute>()
                .Select(x => x.Name)
                // .Select(x => x.Name.Split('|').First())
                .Select(x => x.Trim())
                .ToArray();

            return switches;
        }

        public static string Run(this string exe, string args = null, bool ignoreOutput = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Debug.WriteLine($"Run: {exe} {args}");

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (ignoreOutput)
                    return "";
                else
                    return process.StandardOutput.ReadToEnd().Trim();
            }
        }

        public static string RunWithDelayedInput(this string exe, string args = null, string input = "", int delay = 0)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Debug.WriteLine($"Run: {exe} {args}");

            using (var process = Process.Start(startInfo))
            {
                Task.Delay(delay).ContinueWith(_ =>
                {
                    process.StandardInput.WriteLine(input);
                    foreach (var c in input)
                        process.StandardInput.Write(c);
                    process.StandardInput.Close();
                });

                process.WaitForExit();
                return process.StandardOutput.ReadToEnd().Trim();
            }
        }
    }
}