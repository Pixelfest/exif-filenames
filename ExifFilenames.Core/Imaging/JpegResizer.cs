using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ExifFilenames.Core.Imaging;

/// <summary>
/// Result of a single resize-and-save operation.
/// </summary>
public record JpegResizeResult(int OriginalWidth, int OriginalHeight, int FinalWidth, int FinalHeight)
{
    public bool Resized => OriginalWidth != FinalWidth || OriginalHeight != FinalHeight;
}

/// <summary>
/// Loads an image, normalizes its orientation, shrinks it to fit a maximum dimension
/// (Lanczos3, aspect ratio preserved) and saves it as a JPEG.
/// </summary>
public static class JpegResizer
{
    /// <param name="stripMetadata">
    /// If true, all EXIF, IPTC and XMP metadata (camera, GPS, dates, ...) is removed from the output.
    /// The ICC color profile is kept so colors render correctly.
    /// </param>
    public static async Task<JpegResizeResult> ResizeToJpegAsync(
        string inputPath,
        string outputPath,
        int maxDimension,
        int jpegQuality,
        bool stripMetadata,
        CancellationToken cancellationToken = default)
    {
        if (maxDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDimension), "Max dimension must be greater than 0.");

        using var image = await Image.LoadAsync(inputPath, cancellationToken);

        // Bake the EXIF orientation into the pixels so the result looks right without metadata
        image.Mutate(x => x.AutoOrient());

        int origW = image.Width;
        int origH = image.Height;

        if (origW > maxDimension || origH > maxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        if (stripMetadata)
        {
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            foreach (var frame in image.Frames)
            {
                frame.Metadata.ExifProfile = null;
                frame.Metadata.IptcProfile = null;
                frame.Metadata.XmpProfile = null;
            }
        }

        var encoder = new JpegEncoder { Quality = Math.Clamp(jpegQuality, 1, 100) };
        await image.SaveAsJpegAsync(outputPath, encoder, cancellationToken);

        return new JpegResizeResult(origW, origH, image.Width, image.Height);
    }
}
