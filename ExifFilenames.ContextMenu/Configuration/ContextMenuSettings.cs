using ExifFilenames.Core.Models;

namespace ExifFilenames.ContextMenu.Configuration;

/// <summary>
/// Settings for the Windows Explorer context menu image processing workflow.
/// </summary>
public class ContextMenuSettings
{
    /// <summary>
    /// Maximum width or height in pixels for resized images.
    /// Default is 1920.
    /// </summary>
    public int MaxImageDimension { get; set; } = PipelineOptions.DefaultMaxImageDimension;

    /// <summary>
    /// JPEG quality percentage (1-100) for saved images.
    /// Default is 95.
    /// </summary>
    public int JpegQuality { get; set; } = PipelineOptions.DefaultJpegQuality;

    /// <summary>
    /// Suffix for the copied folder destination.
    /// Default is " - Verkleind".
    /// </summary>
    public string TargetFolderSuffix { get; set; } = " - Verkleind";
}

