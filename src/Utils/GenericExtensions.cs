//css_ref IconExtractor.dll
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
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

    public static string ArgValue(this string[] args, string name)
        => args.FirstOrDefault(x => x.StartsWith($"{name}:"))?.Split(new[] { ':' }, 2).LastOrDefault();

    public static (bool isWin, bool is64) GetPeInfo(this string exe)
    {
        using (var stream = File.OpenRead(exe))
        using (var peFile = new PEReader(stream))
            return (!peFile.PEHeaders.IsConsoleApplication, peFile.PEHeaders.CoffHeader.Machine == Machine.Amd64);
    }
}