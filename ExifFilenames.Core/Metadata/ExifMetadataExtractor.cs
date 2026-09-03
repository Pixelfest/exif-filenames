using System;
using System.IO;
using System.Linq;
using ExifFilenames.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ExifFilenames.Core.Metadata;

/// <summary>
/// Extracts EXIF metadata using MetadataExtractor library.
/// </summary>
public class ExifMetadataExtractor : IExifMetadataExtractor
{
    public PhotoMetadata Extract(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        DateTime? dateOriginal = null;
        DateTime? dateTimeTaken = null;
        string? make = null;
        string? model = null;

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            var exifSubIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            if (exifSubIfd != null)
            {
                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dto))
                {
                    dateOriginal = dto;
                }

                if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dt))
                {
                    dateTimeTaken = dt;
                }
            }

            if (exifSubIfd0 != null)
            {
                make = exifSubIfd0.GetDescription(ExifDirectoryBase.TagMake);
                model = exifSubIfd0.GetDescription(ExifDirectoryBase.TagModel);
            }
        }
        catch
        {
            // If reading EXIF fails (unsupported image, corrupted headers), fall back to file system timestamps.
        }

        DateTime? fileDateTime = null;
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            var created = fileInfo.CreationTime;
            var modified = fileInfo.LastWriteTime;
            fileDateTime = modified < created ? modified : created;
        }

        return new PhotoMetadata
        {
            DateOriginal = dateOriginal,
            DateTimeTaken = dateTimeTaken,
            FileDateTime = fileDateTime,
            Make = make?.Trim(),
            Model = model?.Trim()
        };
    }
}

