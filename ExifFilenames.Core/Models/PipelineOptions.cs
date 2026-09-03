namespace ExifFilenames.Core.Models;

/// <summary>
/// Configuration options for the file processing pipeline.
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// If true, runs the pipeline without modifying, moving, or copying any files.
    /// </summary>
    public bool IsDryRun { get; set; } = false;

    /// <summary>
    /// Whether to traverse subdirectories.
    /// </summary>
    public bool Recurse { get; set; } = false;

    /// <summary>
    /// Minimum file size in bytes to process. Files smaller than this will be skipped.
    /// Default is 30KB (30720 bytes).
    /// </summary>
    public long MinFileSizeBytes { get; set; } = 30 * 1024;

    /// <summary>
    /// Supported file extensions (case-insensitive).
    /// </summary>
    public HashSet<string> SupportedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    /// <summary>
    /// When copying the folder, this suffix is appended to the original folder name.
    /// Default is " - Renamed".
    /// </summary>
    public string TargetFolderSuffix { get; set; } = " - Renamed";

    /// <summary>
    /// If true, creates a duplicate folder and copies files with their new names.
    /// If false, renames files in place.
    /// </summary>
    public bool CopyFolder { get; set; } = false;

    /// <summary>
    /// Default maximum width or height in pixels for resized images.
    /// </summary>
    public const int DefaultMaxImageDimension = 1920;

    /// <summary>
    /// Default JPEG quality percentage (1-100) for saved images.
    /// </summary>
    public const int DefaultJpegQuality = 95;

    /// <summary>
    /// Explicit target directory if specified. If null and CopyFolder is true,
    /// a folder named '{SourceDirectory}{TargetFolderSuffix}' is used.
    /// </summary>
    public string? DestinationDirectory { get; set; }

    /// <summary>
    /// Maximum width or height in pixels for images processed by the resize step.
    /// Aspect ratio is preserved. Default is 1920.
    /// </summary>
    public int MaxImageDimension { get; set; } = DefaultMaxImageDimension;

    /// <summary>
    /// JPEG quality percentage (1-100) when saving resized or converted images.
    /// Default is 95.
    /// </summary>
    public int JpegQuality { get; set; } = DefaultJpegQuality;
}

