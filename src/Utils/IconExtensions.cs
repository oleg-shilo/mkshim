//css_ref IconExtractor.dll
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using mkshim;
using TsudaKageyu;

static class IconExtensions
{
    public static string ApplyOverlayToIcon(this string iconPath, string outputPath = null)
    {
        if (iconPath == null)
            return null;

        var result = outputPath ?? iconPath;
        Icon originalIcon;

        try
        {
            originalIcon = new Icon(iconPath, new Size(256, 256));
        }
        catch
        {
            Console.WriteLine(
                $"WARNING: The icon resolution had to be reduced to allow inserting the overlay.\n" +
                $"This may happen if the icon was extracted from the exe file. In such cases either use stand alone icon or disable overlay with `--no-overlay`.\n");

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

    static Icon[] GetLongestSequenceOfSizes(this Icon[] icons)
    {
        // This is the example of sizes in Notepad first icon
        // 48, 32, 24, 16, 48, 32, 24, 16, 256, 48, 32, 24, 16
        // we want last 5 items as they have the most amount of sizes so have the highest resolution
        // this is the only way as we cannot know the pixel format of the icon

        // bitsPerPixel is always the same
        //      Bitmap bmp = icon.ToBitmap())
        //      PixelFormat format = bmp.PixelFormat;
        //      int bitsPerPixel = Image.GetPixelFormatSize(format);
        //
        try
        {
            var result = new List<List<Icon>>();
            var current = new List<Icon> { icons[0] };
            int? lastDirection = null;

            for (int i = 1; i < icons.Length; i++)
            {
                var diff = icons[i].Size.Width - icons[i - 1].Size.Width;
                int direction = Math.Sign(diff);

                if (lastDirection == null || direction == lastDirection || direction == 0)
                {
                    current.Add(icons[i]);
                }
                else
                {
                    if (current.Count > 1)
                        result.Add(current);

                    current = new List<Icon> { icons[i] };
                    lastDirection = null;
                    continue;
                }

                if (direction != 0)
                    lastDirection = direction;
            }
            result.Add(current); // final one

            var iconWithMostSizes = result.OrderByDescending(x => x.Count).First().ToArray();
            return iconWithMostSizes;
        }
        catch
        {
            return icons;
        }
    }

    static List<Bitmap> ExtractBitmapsFromIcon(Icon icon)
    {
        List<Bitmap> bitmaps = new List<Bitmap>();

        var input = IconUtil.Split(icon).GetLongestSequenceOfSizes();

        // if we failed to identify the sequence we can have a mixture of sizes with duplications
        var items = input
            .Select(x => new { Size = x.Size.Width, Icon = x })
            .GroupBy(x => x.Size)
            .Select(x => x.First().Icon);

        Dictionary<int, Icon> iconSizes = items.OrderBy(x => x.Size.Width).ToDictionary(x => x.Size.Width, y => y);

        foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            var matchingSizeIcon = iconSizes.SkipWhile(x => x.Key < size).FirstOrDefault().Value ?? iconSizes.Last().Value;

            bitmaps.Add(new Bitmap(matchingSizeIcon.ToBitmap(), new Size(size, size)));
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

    public static void SaveIconToFile(this Icon icon, string outputPath)
    {
        using (FileStream fs = new FileStream(outputPath, FileMode.Create))
        {
            icon.Save(fs);
        }
    }

    public static string ExtractFirstIconToFolder(this string binFilePath, string outDir, bool handeErrors = false)
    {
        string iconFile = Path.Combine(outDir, Path.GetFileNameWithoutExtension(binFilePath) + ".ico");

        try
        {
            // Cannot use ExtractAssociatedIconas it extracts only first image of the icon.
            // Thus all other (higher resolution) images of the icon will be lost.
            // So using IconExtractor instead.
            // Considered alternatives:
            // |                                     |                                                                          |
            // |---                                  |---                                                                       |
            // | IconLib                             | works well but somewhat degrades the resolution.                         |
            // | Vanara                              | does not work                                                            |
            // | ManagedShell                        | works completely looses the resolution of the icon                       |
            // | AlphaIconExtractor                  | requires Python                                                          |
            // | Windows API Code Pack               | possibly works but has an enormous size; the product is discontinued     |
            // | Nirsoft IconsExt.exe                | cannot extract icons embedded with rcedit.exe; degrades the resolution   |
            // | 7z.exe                              | does not support handling and only offers resource navigation            |
            // | System...Icon.ExtractAssociatedIcon | loses all multi-resolution info                                          |
            // | Custom implementation               | tried, but too tedious                                                   |
            // Icon icon = Icon.ExtractAssociatedIcon(binFilePath);
            // using (var stream = new System.IO.FileStream(iconFile, System.IO.FileMode.Create))
            //     icon.Save(stream);

            // Toolbelt.IconExtractor nuget package is a later version of TsudaKageyu.IconExtractor
            // and has a bug in the Extract1stIconTo method. It does not extract the icon correctly if it was embedded into exe with rcedit.exe
            // using (var s = File.Create(iconFile))
            //     IconExtractor.Extract1stIconTo(binFilePath, s);

            // This is rather excellent TsudaKageyu.IconExtractor
            // It works great but it has no nuget package so integrating it as a source code since the licence allows that.
            // https://github.com/TsudaKageyu/IconExtractor.git

            var extractor = new IconExtractor(binFilePath);
            if (extractor.Count > 0)
            {
                using (var icon = extractor.GetIcon(0)) // get first icon
                    icon.SaveIconToFile(iconFile);

                using (var validIcon = Bitmap.FromFile(iconFile)) // check that it is a valid icon file
                { }

                return iconFile;
            }
        }
        catch { }

        if (handeErrors)
            return null;
        else
            throw new ApplicationException($"Cannot extract icon from '{iconFile}'");
    }
}