using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace mkshim.tests
{
    public class CLITestBase
    {
        public string mkshim_ico => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "logo.ico"));
        public string mkshim_overlay_32 => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "images", "overlay_32.png"));
        public string mkshim_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "distro", "mkshim.exe"));
        public string mkshim_version => typeof(CLITests).Assembly.GetName().Version.ToString();
        public string target_exe => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestTargetApp", "targetapp.exe"));
        public string target_version => "1.1.1.0";
    }

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
        public static void FilesAreNotSame(this string path1, string path2, [CallerMemberName] string caller = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = -1)
        {
            Assert.True(File.Exists(path1), $"Assert.FilesAreSame failure; File `{path1}` not found: {caller}\n{file} (line {line})");
            Assert.True(File.Exists(path2), $"Assert.FilesAreSame failure; File `{path2}` not found: {caller}\n{file} (line {line})");

            var data1 = File.ReadAllBytes(path1);
            var data2 = File.ReadAllBytes(path2);

            if (data1.Length != data2.Length)
                return;

            for (int i = 0; i < data1.Length; i++)
            {
                if (data1[i] != data2[i])
                {
                    return; // files are different
                }
            }

            Assert.True(false, $"Assert.FilesAreNotSame failure; File `{path1}` and `{path2}` have the same content: {caller}\n{file} (line {line})");
        }

        public static void FilesAreSame(this string path1, string path2, [CallerMemberName] string caller = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = -1)
        {
            Assert.True(File.Exists(path1), $"Assert.FilesAreSame failure; File `{path1}` not found: {caller}\n{file} (line {line})");
            Assert.True(File.Exists(path2), $"Assert.FilesAreSame failure; File `{path2}` not found: {caller}\n{file} (line {line})");

            var data1 = File.ReadAllBytes(path1);
            var data2 = File.ReadAllBytes(path2);

            Assert.True(data1.Length == data2.Length, $"Assert.FilesAreSame failure; File `{path1}` and `{path2}` have different sizes: {caller}\n{file} (line {line})");
            for (int i = 0; i < data1.Length; i++)
            {
                if (data1[i] != data2[i])
                {
                    Assert.True(false, $"Assert.FilesAreSame failure; File `{path1}` and `{path2}` have different content: {caller}\n{file} (line {line})");
                }
            }
        }

        public static void FileExists(this string path, [CallerMemberName] string caller = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = -1)
        {
            Assert.True(File.Exists(path), $"Assert.FileExists failure: {caller}\n{file} (line {line})");
        }
    }

    static class ImageTestExtensions
    {
        public static string GetIconFirstBitmap(this string path, string outFile = null)
        {
            var bitmap = new Icon(path).ToBitmap();
            var pngFile = outFile ?? path + ".1.png";
            bitmap.Save(pngFile, System.Drawing.Imaging.ImageFormat.Png);
            return pngFile;
        }

        public static string GetFirstIconBitmap(this string exeFile, string outFile = null)
        {
            var extractor = new TsudaKageyu.IconExtractor(exeFile);
            if (extractor.Count > 0)
            {
                using (var icon = extractor.GetIcon(0)) // get first icon
                {
                    var pngFile = outFile ?? exeFile + ".1.png";

                    using var bitmap = icon.ToBitmap();
                    bitmap.Save(pngFile, System.Drawing.Imaging.ImageFormat.Png);
                    return pngFile;
                }
            }
            return null;
        }

        /// <summary>
        /// Crop the 32x32 bitmap to 11x11 of the bottom left corner where the overlay is. It's 11 but not 12 because one
        /// outer pixel of the overlay is alpha channeled so it cannot be used for search
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="outputPath">The output path.</param>
        /// <returns></returns>
        public static string CropLeftBottomOverlayAndSaveTo(this string file, string outputPath)
            => file.CropAndSave(1, 20, 11, 11, outputPath);

        public static string CropAndSave(this string file, double left, double top, double width, double height, string outputPath)
        {
            var bitmap = new Bitmap(file);

            int cropX = (int)(left);
            int cropY = (int)(top);
            int cropW = (int)(width);
            int cropH = (int)(height);

            Rectangle cropArea = new Rectangle(cropX, cropY, cropW, cropH);

            using Bitmap cropped = bitmap.Clone(cropArea, bitmap.PixelFormat);
            cropped.Save(outputPath, ImageFormat.Png);

            return outputPath;
        }

        public static bool IsInImageQuadrant(this string patternPath, string mainPath, SearchQuadrant quadrant)
        {
            using var main = new Bitmap(mainPath);
            using var pattern = new Bitmap(patternPath);

            int halfWidth = main.Width / 2;
            int halfHeight = main.Height / 2;

            int startX = quadrant == SearchQuadrant.BottomLeft ? 0 : halfWidth;
            int startY = halfHeight;

            int maxSearchX = Math.Min(main.Width - pattern.Width, startX + halfWidth - 1);
            int maxSearchY = main.Height - pattern.Height;

            for (int x = startX; x <= maxSearchX; x++)
            {
                for (int y = startY; y <= maxSearchY; y++)
                {
                    if (MatchAt(main, pattern, x, y))
                        return true;
                }
            }

            return false;
        }

        private static bool MatchAt(Bitmap haystack, Bitmap needle, int startX, int startY)
        {
            for (int x = 0; x < needle.Width; x++)
            {
                for (int y = 0; y < needle.Height; y++)
                {
                    if (haystack.GetPixel(startX + x, startY + y) != needle.GetPixel(x, y))
                        return false;
                }
            }
            return true;
        }
    }

    public enum SearchQuadrant
    {
        BottomLeft,
        BottomRight
    }

    public static class TestExtensions
    {
        public static bool HasText(this string text) => !string.IsNullOrEmpty(text);

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
                .Select(x => x.Trim())
                .ToArray();

            return switches;
        }

        public static string Run(this string exe, string args = null)
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
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output;
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