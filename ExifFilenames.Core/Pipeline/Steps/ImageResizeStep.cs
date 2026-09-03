using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ExifFilenames.Core.Pipeline.Steps;

/// <summary>
/// Pipeline step that resizes processed images to a maximum width or height while
/// preserving aspect ratio with high quality (Lanczos3), and saves them as 95% quality JPEGs
/// while preserving EXIF metadata.
/// </summary>
public class ImageResizeStep : IPipelineStep
{
    private readonly int? _maxDimension;
    private readonly int? _jpegQuality;

    public string Name => "Resize Images (Max Dimension & JPEG Quality)";

    public ImageResizeStep()
    {
    }

    public ImageResizeStep(int maxDimension, int jpegQuality)
    {
        if (maxDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDimension), "Max dimension must be greater than 0.");
        if (jpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(jpegQuality), "JPEG quality must be between 1 and 100.");

        _maxDimension = maxDimension;
        _jpegQuality = jpegQuality;
    }

    public async Task ExecuteAsync(PipelineContext context, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        int maxDimension = _maxDimension ?? context.Options.MaxImageDimension;
        int jpegQuality = Math.Clamp(_jpegQuality ?? context.Options.JpegQuality, 1, 100);

        var candidateFiles = context.Files
            .Where(f => f.Status == FileStatus.Processed &&
                        context.Options.SupportedExtensions.Contains(Path.GetExtension(f.CurrentPath)))
            .ToList();

        if (candidateFiles.Count == 0)
        {
            progress?.Report(new StepProgress("No processed image files to resize.", ProgressSeverity.Info, Name));
            return;
        }

        progress?.Report(new StepProgress(
            $"Resizing images to max {maxDimension}px @ {jpegQuality}% JPEG quality...",
            ProgressSeverity.Info,
            Name));

        int index = 0;
        int total = candidateFiles.Count;

        foreach (var file in candidateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            if (!File.Exists(file.CurrentPath))
            {
                progress?.Report(new StepProgress(
                    $"[SKIP] File not found: {file.CurrentFileName}",
                    ProgressSeverity.Warning,
                    Name, index, total));
                continue;
            }

            if (context.Options.IsDryRun)
            {
                try
                {
                    var imageInfo = await Image.IdentifyAsync(file.CurrentPath, cancellationToken);
                    if (imageInfo == null)
                    {
                        progress?.Report(new StepProgress(
                            $"[SKIP] Unable to identify image: {file.CurrentFileName}",
                            ProgressSeverity.Warning,
                            Name, index, total));
                        continue;
                    }

                    int origW = imageInfo.Width;
                    int origH = imageInfo.Height;

                    if (origW > maxDimension || origH > maxDimension)
                    {
                        int targetW, targetH;
                        if (origW >= origH)
                        {
                            targetW = maxDimension;
                            targetH = Math.Max(1, (int)Math.Round((double)origH * maxDimension / origW));
                        }
                        else
                        {
                            targetH = maxDimension;
                            targetW = Math.Max(1, (int)Math.Round((double)origW * maxDimension / origH));
                        }

                        progress?.Report(new StepProgress(
                            $"[TEST] Would resize: {file.CurrentFileName} ({origW}x{origH} -> {targetW}x{targetH} @ {jpegQuality}% JPG)",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                    else
                    {
                        progress?.Report(new StepProgress(
                            $"[TEST] Would save as {jpegQuality}% JPG: {file.CurrentFileName} ({origW}x{origH})",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report(new StepProgress(
                        $"[WARN] Error inspecting {file.CurrentFileName}: {ex.Message}",
                        ProgressSeverity.Warning,
                        Name, index, total));
                }
                continue;
            }

            string? tempFile = null;
            try
            {
                using (var image = await Image.LoadAsync(file.CurrentPath, cancellationToken))
                {
                    // Normalize rotation based on EXIF orientation tag so dimensions match physical viewing
                    image.Mutate(x => x.AutoOrient());

                    int origW = image.Width;
                    int origH = image.Height;
                    bool needsResize = origW > maxDimension || origH > maxDimension;

                    if (needsResize)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(maxDimension, maxDimension),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Lanczos3
                        }));
                    }

                    int finalW = image.Width;
                    int finalH = image.Height;

                    var encoder = new JpegEncoder
                    {
                        Quality = jpegQuality
                    };

                    var currentDir = Path.GetDirectoryName(file.CurrentPath) ?? context.DestinationDirectory;
                    tempFile = Path.Combine(currentDir, $".tmp_{Guid.NewGuid():N}.jpg");

                    await image.SaveAsJpegAsync(tempFile, encoder, cancellationToken);

                    var targetJpgPath = Path.ChangeExtension(file.CurrentPath, ".jpg");

                    if (string.Equals(targetJpgPath, file.CurrentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(tempFile, file.CurrentPath, overwrite: true);
                        tempFile = null;
                    }
                    else
                    {
                        File.Move(tempFile, targetJpgPath, overwrite: true);
                        tempFile = null;
                        if (File.Exists(file.CurrentPath))
                        {
                            try { File.Delete(file.CurrentPath); } catch { }
                        }
                        file.CurrentPath = targetJpgPath;
                        file.TargetPath = targetJpgPath;
                    }

                    file.Properties["Resized"] = needsResize;
                    file.Properties["ImageWidth"] = finalW;
                    file.Properties["ImageHeight"] = finalH;
                    file.Properties["JpegQuality"] = jpegQuality;

                    if (needsResize)
                    {
                        progress?.Report(new StepProgress(
                            $"[RESIZED] {file.CurrentFileName} ({origW}x{origH} -> {finalW}x{finalH} @ {jpegQuality}% JPG)",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                    else
                    {
                        progress?.Report(new StepProgress(
                            $"[SAVED] {file.CurrentFileName} ({origW}x{origH} @ {jpegQuality}% JPG)",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report(new StepProgress(
                    $"[ERROR] Failed to resize {file.CurrentFileName}: {ex.Message}",
                    ProgressSeverity.Error,
                    Name, index, total));
            }
            finally
            {
                if (tempFile != null && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}

