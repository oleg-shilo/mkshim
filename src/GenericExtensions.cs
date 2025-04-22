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

static class IconExtensions
{
    public static string ApplyOverlayToIcon(string iconPath, string outputPath = null)
    {
        var result = outputPath ?? iconPath;
        Icon originalIcon;

        try
        {
            originalIcon = new Icon(iconPath, new Size(256, 256));
        }
        catch
        {
            Console.WriteLine(
                $"The overlay could not be applied to the requested shim icon so the icon resolution had to be reduced to allow inserting the overlay.\n" +
                $"This can happen if the icon was extracted from the exe file. In such cases either use stand alone icon or disable overlay with `--no-overlay`");

            originalIcon = new Icon(iconPath);
        }

        using (originalIcon)
        {
            List<Bitmap> bitmaps = ExtractBitmapsFromIcon(originalIcon);
            var overlayImages = LoadOverlayImages();

            List<Bitmap> modifiedBitmaps = bitmaps.Select(bmp => ApplyOverlay(bmp, overlayImages)).ToList();

            using (var newIcon = CreateIconFromBitmaps(modifiedBitmaps))
            {
                SaveIconToFile(newIcon, result);
            }
        }

        return result;
    }

    static List<Bitmap> ExtractBitmapsFromIcon(Icon icon)
    {
        List<Bitmap> bitmaps = new List<Bitmap>();
        foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            bitmaps.Add(new Bitmap(icon.ToBitmap(), new Size(size, size)));
        }
        return bitmaps;
    }

    static Dictionary<int, Bitmap> LoadOverlayImages()
    {
        return new Dictionary<int, Bitmap>
        {
            { 16, Resource1.overlay_16 },
            { 24, Resource1.overlay_24 },
            { 32, Resource1.overlay_32  },
            { 48, Resource1.overlay_48 },
            { 64, Resource1.overlay_64 },
            { 128, Resource1.overlay_128 },
            { 256, Resource1.overlay_256 },
        };
    }

    static Bitmap ToBitmap(this byte[] data)
    {
        using (var ms = new MemoryStream(data))
        {
            return new Bitmap(ms);
        }
    }

    public static Bitmap ApplyOverlay(Bitmap baseImage, Dictionary<int, Bitmap> overlays)
    {
        if (!overlays.TryGetValue(baseImage.Width, out var overlay))
        {
            return baseImage; // No overlay found for this size
        }

        Bitmap result = new Bitmap(baseImage);
        using (Graphics g = Graphics.FromImage(result))
        {
            int x = baseImage.Width - overlay.Width;
            int y = baseImage.Height - overlay.Height;
            g.DrawImage(overlay, new Rectangle(x, y, overlay.Width, overlay.Height));
        }
        return result;
    }

    static Icon CreateIconFromBitmaps(List<Bitmap> bitmaps)
    {
        using (var ms = new MemoryStream())
        {
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((short)0); // Reserved
                writer.Write((short)1); // Type: Icon
                writer.Write((short)bitmaps.Count);

                int dataOffset = 6 + (bitmaps.Count * 16);
                List<byte[]> imageDataList = new List<byte[]>();

                foreach (Bitmap bmp in bitmaps)
                {
                    byte[] bmpData = GetBitmapData(bmp);
                    writer.Write((byte)bmp.Width);
                    writer.Write((byte)bmp.Height);
                    writer.Write((byte)0); // No color palette
                    writer.Write((byte)0);
                    writer.Write((short)1);
                    writer.Write((short)32);
                    writer.Write(bmpData.Length);
                    writer.Write(dataOffset);
                    imageDataList.Add(bmpData);
                    dataOffset += bmpData.Length;
                }

                foreach (byte[] imageData in imageDataList)
                {
                    writer.Write(imageData);
                }
            }

            return new Icon(new MemoryStream(ms.ToArray()));
        }
    }

    static byte[] GetBitmapData(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    static void SaveIconToFile(Icon icon, string outputPath)
    {
        using (FileStream fs = new FileStream(outputPath, FileMode.Create))
        {
            icon.Save(fs);
        }
    }
}