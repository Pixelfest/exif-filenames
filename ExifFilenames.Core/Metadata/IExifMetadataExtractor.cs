using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Metadata;

/// <summary>
/// Service interface for extracting EXIF metadata from image files.
/// </summary>
public interface IExifMetadataExtractor
{
    /// <summary>
    /// Extracts photo metadata from the specified file path.
    /// </summary>
    PhotoMetadata Extract(string filePath);
}

