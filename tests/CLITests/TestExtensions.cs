using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace mkshim.tests
{
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

        public static string Run(this string exe, string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.StandardOutput.ReadToEnd().Trim();
            }
        }
    }
}