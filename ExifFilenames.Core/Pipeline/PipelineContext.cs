using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExifFilenames.Core.Metadata;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Naming;

namespace ExifFilenames.Core.Pipeline;

/// <summary>
/// Encapsulates all state, options, and files for a single pipeline run.
/// </summary>
public class PipelineContext
{
    public string SourceDirectory { get; }
    public string DestinationDirectory { get; set; }
    public PipelineOptions Options { get; }
    public IExifMetadataExtractor MetadataExtractor { get; }
    public IFileNameGenerator FileNameGenerator { get; }

    public List<FileItem> Files { get; } = new();
    public Dictionary<string, object?> Items { get; } = new(StringComparer.OrdinalIgnoreCase);

    public PipelineContext(
        string sourceDirectory,
        PipelineOptions? options = null,
        IExifMetadataExtractor? metadataExtractor = null,
        IFileNameGenerator? fileNameGenerator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        SourceDirectory = Path.GetFullPath(sourceDirectory);
        Options = options ?? new PipelineOptions();
        MetadataExtractor = metadataExtractor ?? new ExifMetadataExtractor();
        FileNameGenerator = fileNameGenerator ?? new ExifFileNameGenerator();

        if (Options.CopyFolder)
        {
            DestinationDirectory = !string.IsNullOrWhiteSpace(Options.DestinationDirectory)
                ? Path.GetFullPath(Options.DestinationDirectory)
                : ComputeUniqueCopyFolderName(SourceDirectory, Options.TargetFolderSuffix);
        }
        else
        {
            DestinationDirectory = SourceDirectory;
        }
    }

    /// <summary>
    /// Computes a non-colliding copy folder name such as "MyPhotos - Renamed" or "MyPhotos - Renamed (2)".
    /// </summary>
    public static string ComputeUniqueCopyFolderName(string sourcePath, string suffix)
    {
        if (!string.IsNullOrEmpty(suffix) && !suffix.StartsWith(" ") && suffix.StartsWith("-"))
        {
            suffix = " " + suffix;
        }

        var parentDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var folderName = Path.GetFileName(sourcePath);
        var baseTarget = Path.Combine(parentDir, $"{folderName}{suffix}");

        if (!Directory.Exists(baseTarget))
            return baseTarget;

        int index = 2;
        while (true)
        {
            var candidate = Path.Combine(parentDir, $"{folderName}{suffix} ({index})");
            if (!Directory.Exists(candidate))
                return candidate;
            index++;
        }
    }

    public int TotalCount => Files.Count;
    public int ProcessedCount => Files.Count(f => f.Status == FileStatus.Processed);
    public int SkippedCount => Files.Count(f => f.Status == FileStatus.Skipped);
    public int FailedCount => Files.Count(f => f.Status == FileStatus.Failed);
}

