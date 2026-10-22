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
    public string TargetFolderSuffix { get; set; } = DefaultTargetFolderSuffix;

    /// <summary>
    /// Suffix for resized copies of individually selected files,
    /// placed next to the original (e.g. "IMG_0001 - verkleind.jpg").
    /// Default is " - verkleind".
    /// </summary>
    public string TargetFileSuffix { get; set; } = DefaultTargetFileSuffix;

    public const string DefaultTargetFolderSuffix = " - Verkleind";
    public const string DefaultTargetFileSuffix = " - verkleind";

    /// <summary>
    /// Ensures a suffix like "-x" gets a leading space; returns <paramref name="fallback"/> when empty.
    /// </summary>
    public static string NormalizeSuffix(string? suffix, string fallback)
    {
        suffix = suffix?.TrimEnd();
        if (string.IsNullOrWhiteSpace(suffix))
            return fallback;
        if (!suffix.StartsWith(" ") && suffix.StartsWith("-"))
            return " " + suffix;
        return suffix;
    }
}

