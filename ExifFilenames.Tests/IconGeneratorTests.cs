using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ExifFilenames.Tests;

public class IconGeneratorTests
{
    [Fact]
    public void GenerateAppIcon_CreatesMultiResolutionIco()
    {
        var baseDir = AppContext.BaseDirectory;
        // Navigate up from bin/Debug/net10.0-windows to solution root
        var solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var targetDir = Path.Combine(solutionDir, "ExifFilenames.ContextMenu", "Assets");
        Directory.CreateDirectory(targetDir);
        var targetFile = Path.Combine(targetDir, "app.ico");

        int[] sizes = [256, 48, 32, 24, 16];
        var pngStreams = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var img = CreateIconImage(size);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            pngStreams.Add(ms.ToArray());
        }

        using var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICO Header
        bw.Write((ushort)0); // Reserved
        bw.Write((ushort)1); // Type 1 = Icon
        bw.Write((ushort)sizes.Length); // Image count

        int offset = 6 + (sizes.Length * 16);

        // Directory entries
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            byte bSize = s >= 256 ? (byte)0 : (byte)s;
            bw.Write(bSize); // Width
            bw.Write(bSize); // Height
            bw.Write((byte)0); // Color count
            bw.Write((byte)0); // Reserved
            bw.Write((ushort)1); // Color planes
            bw.Write((ushort)32); // Bits per pixel
            bw.Write((uint)pngStreams[i].Length); // Size of image data
            bw.Write((uint)offset); // Offset of image data

            offset += pngStreams[i].Length;
        }

        // Image data
        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write(pngStreams[i]);
        }

        Assert.True(File.Exists(targetFile));
        Assert.True(new FileInfo(targetFile).Length > 1000);
    }

    private static Image<Rgba32> CreateIconImage(int size)
    {
        var img = new Image<Rgba32>(size, size);

        // Palette
        var borderCol = new Rgba32(37, 99, 235);     // Blue 600
        var bgCol = new Rgba32(239, 246, 255);       // Blue 50
        var mountainCol = new Rgba32(96, 165, 250);  // Blue 400
        var mountainDark = new Rgba32(59, 130, 246); // Blue 500
        var sunCol = new Rgba32(245, 158, 11);       // Amber 500
        var badgeBg = new Rgba32(16, 185, 129);      // Emerald 500
        var badgeBorder = new Rgba32(255, 255, 255); // White
        var arrowCol = new Rgba32(255, 255, 255);    // White

        float scale = size / 256f;

        // Frame dimensions (photo outline)
        float pad = Math.Max(1, 14 * scale);
        float frameW = 228 * scale;
        float frameH = 200 * scale;
        float cornerRadius = Math.Max(2, 28 * scale);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Photo frame background with rounded corners
                if (x >= pad && x <= pad + frameW && y >= pad && y <= pad + frameH)
                {
                    // Distance from edges for rounded corners
                    float dx = Math.Max(0, Math.Max(pad + cornerRadius - x, x - (pad + frameW - cornerRadius)));
                    float dy = Math.Max(0, Math.Max(pad + cornerRadius - y, y - (pad + frameH - cornerRadius)));
                    if (dx * dx + dy * dy <= cornerRadius * cornerRadius)
                    {
                        // Border vs Fill
                        float borderThickness = Math.Max(1.5f, 14 * scale);
                        bool isBorder = x < pad + borderThickness || x > pad + frameW - borderThickness ||
                                        y < pad + borderThickness || y > pad + frameH - borderThickness;

                        if (isBorder)
                        {
                            img[x, y] = borderCol;
                        }
                        else
                        {
                            img[x, y] = bgCol;

                            // Sun
                            float sunX = 70 * scale;
                            float sunY = 70 * scale;
                            float sunR = 24 * scale;
                            float sDist = (x - sunX) * (x - sunX) + (y - sunY) * (y - sunY);
                            if (sDist <= sunR * sunR)
                            {
                                img[x, y] = sunCol;
                            }

                            // Mountain 1 (back mountain)
                            float m1PeakX = 140 * scale;
                            float m1PeakY = 80 * scale;
                            float m1BaseY = pad + frameH - borderThickness;
                            float slope1 = (m1BaseY - m1PeakY) / (80 * scale);
                            if (y >= m1PeakY && y <= m1BaseY)
                            {
                                float span = (y - m1PeakY) / slope1;
                                if (Math.Abs(x - m1PeakX) <= span)
                                {
                                    img[x, y] = mountainCol;
                                }
                            }

                            // Mountain 2 (front mountain)
                            float m2PeakX = 85 * scale;
                            float m2PeakY = 110 * scale;
                            float slope2 = (m1BaseY - m2PeakY) / (70 * scale);
                            if (y >= m2PeakY && y <= m1BaseY)
                            {
                                float span = (y - m2PeakY) / slope2;
                                if (Math.Abs(x - m2PeakX) <= span)
                                {
                                    img[x, y] = mountainDark;
                                }
                            }
                        }
                    }
                }

                // Badge in bottom-right corner: circle with down-arrow
                float badgeCx = 190 * scale;
                float badgeCy = 185 * scale;
                float badgeR = 56 * scale;
                float bDist = (x - badgeCx) * (x - badgeCx) + (y - badgeCy) * (y - badgeCy);

                if (bDist <= (badgeR + 4 * scale) * (badgeR + 4 * scale))
                {
                    if (bDist > badgeR * badgeR)
                    {
                        img[x, y] = badgeBorder; // White outline ring
                    }
                    else
                    {
                        img[x, y] = badgeBg; // Emerald fill

                        // Down arrow
                        float relX = x - badgeCx;
                        float relY = y - badgeCy;
                        float shaftW = Math.Max(1f, 10 * scale);

                        // Stem of arrow
                        if (Math.Abs(relX) <= shaftW && relY >= -24 * scale && relY <= 8 * scale)
                        {
                            img[x, y] = arrowCol;
                        }

                        // Arrow head triangle
                        float headTopY = 2 * scale;
                        float headBottomY = 26 * scale;
                        float headHalfW = 24 * scale;
                        if (relY >= headTopY && relY <= headBottomY)
                        {
                            float arrowTipSpan = (headBottomY - relY) * (headHalfW / (headBottomY - headTopY));
                            if (Math.Abs(relX) <= arrowTipSpan)
                            {
                                img[x, y] = arrowCol;
                            }
                        }
                    }
                }
            }
        }

        return img;
    }
}
