//css_ref IconExtractor.dll
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using mkshim;
using TsudaKageyu;

static class GenericExtensions
{
    public static string ToRelativePathFrom(this string toFile, string fromDir)
    {
        if (fromDir.Last() != Path.DirectorySeparatorChar || fromDir.Last() != Path.AltDirectorySeparatorChar)
            fromDir = fromDir + Path.DirectorySeparatorChar;

        Uri fromUri = new Uri(fromDir);
        Uri toUri = new Uri(toFile);
        Uri relativeUri = fromUri.MakeRelativeUri(toUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString())
                                 .Replace('/', Path.DirectorySeparatorChar);
        return relativePath;
    }

    public static bool HasText(this string text) => !string.IsNullOrEmpty(text);

    public static string EnsureExtension(this string path, string extension)
    {
        if (string.Compare(Path.GetExtension(path), extension, true) != 0)
            return path + extension;
        else
            return path;
    }

    public static bool IsDirectory(this string path) => Directory.Exists(path);

    public static bool IsValidFilePath(this string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        char[] invalidChars = Path.GetInvalidPathChars();
        return !path.Any(c => invalidChars.Contains(c));
    }

    public static bool HasReadPermissions(this string file)
    {
        try
        {
            using (var stream = File.OpenRead(file))
            { }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static bool HasWritePermissions(this string dir)
    {
        var alreadyExists = Directory.Exists(dir);
        var testFile = Path.Combine(dir, "test.tmp");
        try
        {
            if (!alreadyExists)
                Directory.CreateDirectory(dir);
            else
                File.Create(testFile).Close(); // check if we can write to the directory

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            if (!alreadyExists && Directory.Exists(dir))
                try { Directory.Delete(dir, true); } catch { }

            if (File.Exists(testFile))
                try { File.Delete(testFile); } catch { }
        }
    }

    public static string GetDirName(this string file)
        => Path.GetDirectoryName(file);

    public static string ChangeDir(this string file, string newDir)
        => Path.Combine(newDir, Path.GetFileName(file));

    public static FileVersionInfo GetFileVersion(this string file)
        => FileVersionInfo.GetVersionInfo(file);

    public static string ToCliISwitch(this string args)
    {
        return "";
    }

    public static void ValidateCliArgs(this string[] args)
    {
        var obsoleteSwitches = typeof(RunOptions).GetFields()
            .Select(x => new
            {
                Obsolete = x.GetCustomAttributes(typeof(ObsoleteAttribute), true).Cast<ObsoleteAttribute>().FirstOrDefault()?.Message,
                Args = x.GetCustomAttributes(typeof(CliArgAttribute), true).Cast<CliArgAttribute>()
                    .Select(y => y.Name)
                    .FirstOrDefault()? // there can be only one DescriptionAttribute for a member
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(y => y.Trim())
                    .ToArray()
            })
            .Where(x => x.Obsolete != null && x.Args.Any());

        foreach (var item in obsoleteSwitches)
        {
            foreach (var obsoleteArg in item.Args)
            {
                if (args.Contains(obsoleteArg))
                    throw new ValidationException($"The switch '{obsoleteArg}' is obsolete. {item.Obsolete}");
            }
        }
    }

    public static string GetValueFor(this string[] args, string name)
    {
        var switches = typeof(RunOptions).GetFields()
            .Where(x => x.Name == name)
            .SelectMany(x => x.GetCustomAttributes(typeof(CliArgAttribute), true))
            .Cast<CliArgAttribute>()
            .Select(x => x.Name)
            .FirstOrDefault()? // there can be only one DescriptionAttribute for a member
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim());

        foreach (var item in switches)
        {
            var result = args.ArgValue(item);
            if (!string.IsNullOrEmpty(result))
                return result;
        }

        return default;
    }

    static void sample_HideConsoleWindow()
    {
        var thisApp = Process.GetCurrentProcess();

        if (thisApp.IsDirectlyHostedByExplorer())
        {
            // we can check the name for being "WindowsTerminal" but there is no need since
            // FindVisibleExternalTerminal uses window class name specific for terminal
            // var name = thisApp.FindVisibleExternalTerminal().GetWindowThreadProcess()?.ProcessName;

            thisApp.FindVisibleExternalTerminal().Hide(); // hide the terminal connected to our console; required for Win10+
            ConsoleExtensions.GetConsoleWindow().Hide(); // hide our own console; will handle console on older Windows
        }
    }

    public static bool? HaveArgFor(this string[] args, string name)
    {
        var switches = typeof(RunOptions).GetFields()
            .Where(x => x.Name == name)
            .SelectMany(x => x.GetCustomAttributes(typeof(CliArgAttribute), true))
            .Cast<CliArgAttribute>()
            .Select(x => x.Name)
            .FirstOrDefault()? // there can be only one CliArgAttribute for a member
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim());

        foreach (var item in switches)
        {
            if (args.Contains(item))
                return true;
        }

        return default;
    }

    public static string ArgValue(this string[] args, string name)
        => args.FirstOrDefault(x => x.StartsWith($"{name}:"))?.Split(new[] { ':' }, 2).LastOrDefault();

    public static (bool isWin, bool is64) GetPeInfo(this string exe)
    {
        using (var stream = File.OpenRead(exe))
        using (var peFile = new PEReader(stream))
            return (!peFile.PEHeaders.IsConsoleApplication, peFile.PEHeaders.CoffHeader.Machine == Machine.Amd64);
    }

    public static Process RunCompiler(this string exe, string args, StringBuilder compileLog)
    {
        var p = new Process();
        p.StartInfo.FileName = exe;
        p.StartInfo.Arguments = args;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardError = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardInput = false;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

        p.Start();

        string line;
        while (null != (line = p.StandardOutput.ReadLine()))
        {
            if (line.Trim() != "" && !line.Trim().StartsWith("This compiler is provided as part of the Microsoft (R) .NET Framework,"))
                compileLog.AppendLine("> " + line);
        }
        return p;
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

        using (var process = Process.Start(startInfo))
        {
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }
    }

    public static string ExtractTargetOfShim(this string shimFile)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // cannot run and collect STDOUT as the shim might be a window app
            shimFile.Run($"--mkshim-noop-target \"{tempFile}\""); // deliberately undocummented internal hidden command

            var output = File.ReadAllText(tempFile);
            return output.Trim();
        }
        finally { File.Delete(tempFile); }
    }

    public static string[] ParseCliArgs(this string commandLine)
    {
        var args = new List<string>();
        var currentArg = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(c);
            }
        }
        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }
        return args.ToArray();
    }
}

class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}