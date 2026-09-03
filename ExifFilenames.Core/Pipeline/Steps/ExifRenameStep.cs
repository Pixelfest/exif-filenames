using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Pipeline.Steps;

/// <summary>
/// Pipeline step that inspects EXIF metadata, generates new filenames,
/// and moves or copies files to their target paths.
/// </summary>
public class ExifRenameStep : IPipelineStep
{
    public string Name => "EXIF Metadata & Rename";

    public async Task ExecuteAsync(PipelineContext context, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        int index = 0;
        int total = context.Files.Count;

        // Keep track of reserved target filenames during this batch run to avoid collision race conditions
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in context.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            var extension = Path.GetExtension(file.SourcePath);
            if (!context.Options.SupportedExtensions.Contains(extension))
            {
                if (context.Options.CopyFolder)
                {
                    // Copy non-supported files as-is to preserve directory contents
                    var relDir = Path.GetRelativePath(context.SourceDirectory, Path.GetDirectoryName(file.SourcePath) ?? string.Empty);
                    var targetDir = Path.Combine(context.DestinationDirectory, relDir);
                    var targetFile = Path.Combine(targetDir, Path.GetFileName(file.SourcePath));

                    file.TargetPath = targetFile;
                    if (!context.Options.IsDryRun)
                    {
                        Directory.CreateDirectory(targetDir);
                        await CopyFileAsync(file.SourcePath, targetFile, cancellationToken);
                        file.CurrentPath = targetFile;
                    }

                    file.Status = FileStatus.Skipped;
                    file.Message = $"Unsupported format ({extension}), copied as-is";
                    progress?.Report(new StepProgress(
                        $"[COPY-AS-IS] {file.OriginalFileName}",
                        ProgressSeverity.Trace,
                        Name, index, total));
                }
                else
                {
                    file.Status = FileStatus.Skipped;
                    file.Message = $"Unsupported format ({extension})";
                    progress?.Report(new StepProgress(
                        $"[SKIP] Unsupported format ({extension}): {file.OriginalFileName}",
                        ProgressSeverity.Trace,
                        Name, index, total));
                }
                continue;
            }

            var fileInfo = new FileInfo(file.CurrentPath);
            if (!fileInfo.Exists)
            {
                file.Status = FileStatus.Failed;
                file.Message = "File not found.";
                progress?.Report(new StepProgress(
                    $"[ERROR] File not found: {file.OriginalFileName}",
                    ProgressSeverity.Error,
                    Name, index, total));
                continue;
            }

            if (fileInfo.Length < context.Options.MinFileSizeBytes)
            {
                file.Status = FileStatus.Skipped;
                file.Message = $"File too small ({fileInfo.Length} bytes < {context.Options.MinFileSizeBytes} bytes).";
                progress?.Report(new StepProgress(
                    $"[SKIP] File too small: {file.OriginalFileName}",
                    ProgressSeverity.Warning,
                    Name, index, total));
                continue;
            }

            // Extract metadata
            file.Metadata = context.MetadataExtractor.Extract(file.CurrentPath);

            // Determine target directory for this file (preserving subdirectories)
            string targetDirectory;
            if (context.Options.CopyFolder)
            {
                var relDir = Path.GetRelativePath(context.SourceDirectory, Path.GetDirectoryName(file.SourcePath) ?? string.Empty);
                targetDirectory = Path.Combine(context.DestinationDirectory, relDir);
            }
            else
            {
                targetDirectory = Path.GetDirectoryName(file.CurrentPath) ?? context.SourceDirectory;
            }

            // Compute unique filename avoiding existing disk files AND already reserved files in this batch
            string targetPath;
            try
            {
                targetPath = context.FileNameGenerator.GetUniqueFilePath(
                    targetDirectory,
                    file.Metadata,
                    extension,
                    path => File.Exists(path) || reservedPaths.Contains(path));
            }
            catch (Exception ex)
            {
                file.Status = FileStatus.Failed;
                file.Message = ex.Message;
                progress?.Report(new StepProgress(
                    $"[FAIL] Failed to generate name for {file.OriginalFileName}: {ex.Message}",
                    ProgressSeverity.Error,
                    Name, index, total));
                continue;
            }

            reservedPaths.Add(targetPath);
            file.TargetPath = targetPath;
            var targetFileName = Path.GetFileName(targetPath);

            // Check if already renamed
            if (string.Equals(file.OriginalFileName, targetFileName, StringComparison.OrdinalIgnoreCase) && !context.Options.CopyFolder)
            {
                file.Status = FileStatus.Skipped;
                file.Message = "File was already renamed, no change.";
                progress?.Report(new StepProgress(
                    $"[SKIP] Already renamed: {file.OriginalFileName}",
                    ProgressSeverity.Warning,
                    Name, index, total));
                continue;
            }

            if (context.Options.IsDryRun)
            {
                file.Status = FileStatus.Processed;
                var action = context.Options.CopyFolder ? "Would copy" : "Would rename";
                progress?.Report(new StepProgress(
                    $"[TEST] {action}: {file.OriginalFileName} => {targetFileName}",
                    ProgressSeverity.Success,
                    Name, index, total));
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(targetDirectory);

                    if (context.Options.CopyFolder)
                    {
                        await CopyFileAsync(file.CurrentPath, targetPath, cancellationToken);
                        file.CurrentPath = targetPath; // Set current path to the copy for subsequent pipeline steps
                        file.Status = FileStatus.Processed;
                        progress?.Report(new StepProgress(
                            $"[COPIED] {file.OriginalFileName} => {targetFileName}",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                    else
                    {
                        File.Move(file.CurrentPath, targetPath);
                        file.CurrentPath = targetPath;
                        file.Status = FileStatus.Processed;
                        progress?.Report(new StepProgress(
                            $"[RENAMED] {file.OriginalFileName} => {targetFileName}",
                            ProgressSeverity.Success,
                            Name, index, total));
                    }
                }
                catch (Exception ex)
                {
                    file.Status = FileStatus.Failed;
                    file.Message = ex.Message;
                    progress?.Report(new StepProgress(
                        $"[ERROR] Error processing {file.OriginalFileName}: {ex.Message}",
                        ProgressSeverity.Error,
                        Name, index, total));
                }
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var destStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await sourceStream.CopyToAsync(destStream, cancellationToken);
    }
}

