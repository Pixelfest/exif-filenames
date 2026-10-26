using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Imaging;

/// <summary>
/// Creates a resized, metadata-free JPEG copy next to each selected file,
/// e.g. "IMG_0001.jpg" -> "IMG_0001 - verkleind.jpg". Originals are never modified.
/// </summary>
public class SelectedFilesResizer
{
    public const string StepName = "Resize Selected Files";

    private readonly int _maxDimension;
    private readonly int _jpegQuality;
    private readonly string _fileSuffix;
    private readonly ISet<string> _supportedExtensions;

    public SelectedFilesResizer(int maxDimension, int jpegQuality, string fileSuffix, ISet<string>? supportedExtensions = null)
    {
        if (maxDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDimension), "Max dimension must be greater than 0.");
        if (jpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(jpegQuality), "JPEG quality must be between 1 and 100.");
        if (string.IsNullOrWhiteSpace(fileSuffix))
            throw new ArgumentException("File suffix must not be empty, or the copy would overwrite the original.", nameof(fileSuffix));

        _maxDimension = maxDimension;
        _jpegQuality = jpegQuality;
        _fileSuffix = fileSuffix;
        _supportedExtensions = supportedExtensions ?? new PipelineOptions().SupportedExtensions;
    }

    public async Task<List<FileItem>> ExecuteAsync(
        IEnumerable<string> filePaths,
        IProgress<StepProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = filePaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new FileItem { SourcePath = p, CurrentPath = p })
            .ToList();

        int index = 0;
        int total = files.Count;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            if (!File.Exists(file.SourcePath))
            {
                Skip(file, "File not found.", progress, index, total);
                continue;
            }

            if (!_supportedExtensions.Contains(Path.GetExtension(file.SourcePath)))
            {
                Skip(file, "Not a supported image type.", progress, index, total);
                continue;
            }

            var targetPath = ComputeTargetPath(file.SourcePath, _fileSuffix);
            string? tempFile = null;
            try
            {
                tempFile = Path.Combine(Path.GetDirectoryName(targetPath)!, $".tmp_{Guid.NewGuid():N}.jpg");

                var result = await JpegResizer.ResizeToJpegAsync(
                    file.SourcePath, tempFile, _maxDimension, _jpegQuality, stripMetadata: true, cancellationToken);

                File.Move(tempFile, targetPath, overwrite: false);
                tempFile = null;

                file.CurrentPath = targetPath;
                file.TargetPath = targetPath;
                file.Status = FileStatus.Processed;
                file.Properties["Resized"] = result.Resized;
                file.Properties["ImageWidth"] = result.FinalWidth;
                file.Properties["ImageHeight"] = result.FinalHeight;

                progress?.Report(new StepProgress(
                    $"[SAVED] {file.OriginalFileName} -> {file.TargetFileName} ({result.FinalWidth}x{result.FinalHeight}, metadata removed)",
                    ProgressSeverity.Success,
                    StepName, index, total));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                file.Status = FileStatus.Failed;
                file.Message = ex.Message;
                progress?.Report(new StepProgress(
                    $"[ERROR] Failed to resize {file.OriginalFileName}: {ex.Message}",
                    ProgressSeverity.Error,
                    StepName, index, total));
            }
            finally
            {
                if (tempFile != null && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Computes a non-colliding output path next to the source, such as
    /// "photo - verkleind.jpg" or "photo - verkleind (2).jpg".
    /// </summary>
    public static string ComputeTargetPath(string sourcePath, string suffix)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        var candidate = Path.Combine(dir, $"{baseName}{suffix}.jpg");
        int index = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(dir, $"{baseName}{suffix} ({index}).jpg");
            index++;
        }
        return candidate;
    }

    private static void Skip(FileItem file, string reason, IProgress<StepProgress>? progress, int index, int total)
    {
        file.Status = FileStatus.Skipped;
        file.Message = reason;
        progress?.Report(new StepProgress(
            $"[SKIP] {file.OriginalFileName}: {reason}",
            ProgressSeverity.Warning,
            StepName, index, total));
    }
}
