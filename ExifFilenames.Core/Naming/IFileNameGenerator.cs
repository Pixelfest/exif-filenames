using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Naming;

/// <summary>
/// Service interface for generating filenames based on photo metadata.
/// </summary>
public interface IFileNameGenerator
{
    /// <summary>
    /// Generates a filename (without directory) for a given photo metadata and sequence number.
    /// </summary>
    string GenerateFileName(PhotoMetadata metadata, int sequenceNumber, string extension);

    /// <summary>
    /// Computes a unique file path within the target directory by incrementing the sequence number
    /// until no file with that name exists.
    /// </summary>
    string GetUniqueFilePath(string targetDirectory, PhotoMetadata metadata, string extension, Func<string, bool>? fileExistsPredicate = null);
}

