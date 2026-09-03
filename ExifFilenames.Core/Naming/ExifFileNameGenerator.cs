using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Naming;

/// <summary>
/// Generates standardized filenames from EXIF metadata.
/// Format: {yyyy-MM-dd HH.mm.ss} [ - {Make} {Model}] - {00001}.{ext}
/// </summary>
public class ExifFileNameGenerator : IFileNameGenerator
{
    private static readonly Regex InvalidCharsRegex;

    public Dictionary<string, string> Replacements { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "NIKON CORPORATION NIKON D5200", "Nikon D5200" },
        { "samsung", "Samsung" }
    };

    static ExifFileNameGenerator()
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        InvalidCharsRegex = new Regex($"[{Regex.Escape(invalidChars)}]", RegexOptions.Compiled);
    }

    public string GenerateFileName(PhotoMetadata metadata, int sequenceNumber, string extension)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var date = metadata.EffectiveDate;
        var sb = new StringBuilder();

        // 1. Date prefix
        sb.Append(date.ToString("yyyy-MM-dd HH.mm.ss"));

        // 2. Camera make and model if available
        if (!string.IsNullOrWhiteSpace(metadata.Make) && !string.IsNullOrWhiteSpace(metadata.Model))
        {
            var cameraString = $"{metadata.Make} {metadata.Model}";
            sb.Append($" - {cameraString}");
        }

        // 3. Sequence number and extension
        var cleanExt = extension.TrimStart('.');
        var candidate = $"{sb} - {sequenceNumber:00000}.{cleanExt}";

        // 4. Strip invalid path/file characters
        candidate = InvalidCharsRegex.Replace(candidate, string.Empty);

        // 5. Apply known camera string replacements
        foreach (var (from, to) in Replacements)
        {
            candidate = candidate.Replace(from, to, StringComparison.OrdinalIgnoreCase);
        }

        return candidate;
    }

    public string GetUniqueFilePath(string targetDirectory, PhotoMetadata metadata, string extension, Func<string, bool>? fileExistsPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(targetDirectory);
        ArgumentNullException.ThrowIfNull(metadata);

        fileExistsPredicate ??= File.Exists;

        for (int sequence = 1; sequence <= 99999; sequence++)
        {
            string fileName = GenerateFileName(metadata, sequence, extension);
            string fullPath = Path.Combine(targetDirectory, fileName);

            if (!fileExistsPredicate(fullPath))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException($"Too many collision files for pattern in '{targetDirectory}'.");
    }
}

