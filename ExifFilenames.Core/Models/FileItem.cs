namespace ExifFilenames.Core.Models;

public enum FileStatus
{
    Pending,
    Processed,
    Skipped,
    Failed
}

/// <summary>
/// Represents a single file being processed through the pipeline.
/// </summary>
public class FileItem
{
    public required string SourcePath { get; init; }
    public required string CurrentPath { get; set; }
    public string? TargetPath { get; set; }
    public PhotoMetadata? Metadata { get; set; }
    public FileStatus Status { get; set; } = FileStatus.Pending;
    public string? Message { get; set; }

    /// <summary>
    /// Custom metadata properties extensible by pipeline steps.
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string OriginalFileName => Path.GetFileName(SourcePath);
    public string CurrentFileName => Path.GetFileName(CurrentPath);
    public string? TargetFileName => TargetPath != null ? Path.GetFileName(TargetPath) : null;
}

