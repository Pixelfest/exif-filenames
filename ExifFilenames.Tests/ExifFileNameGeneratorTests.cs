using System;
using System.IO;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Naming;
using Xunit;

namespace ExifFilenames.Tests;

public class ExifFileNameGeneratorTests
{
    private readonly ExifFileNameGenerator _generator = new();

    [Fact]
    public void GenerateFileName_WithDateAndCamera_FormatsCorrectly()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2023, 7, 15, 14, 30, 45),
            Make = "Canon",
            Model = "EOS 80D"
        };

        var fileName = _generator.GenerateFileName(metadata, 1, ".jpg");

        Assert.Equal("2023-07-15 14.30.45 - Canon EOS 80D - 00001.jpg", fileName);
    }

    [Fact]
    public void GenerateFileName_ReplacesNikonD5200Quirk()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2024, 1, 1, 12, 0, 0),
            Make = "NIKON CORPORATION",
            Model = "NIKON D5200"
        };

        var fileName = _generator.GenerateFileName(metadata, 1, "jpg");

        Assert.Equal("2024-01-01 12.00.00 - Nikon D5200 - 00001.jpg", fileName);
    }

    [Fact]
    public void GenerateFileName_ReplacesSamsungQuirk()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2024, 2, 10, 8, 15, 30),
            Make = "samsung",
            Model = "SM-G991B"
        };

        var fileName = _generator.GenerateFileName(metadata, 2, ".png");

        Assert.Equal("2024-02-10 08.15.30 - Samsung SM-G991B - 00002.png", fileName);
    }

    [Fact]
    public void GenerateFileName_WithoutCamera_UsesOnlyDate()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2022, 11, 20, 18, 5, 0)
        };

        var fileName = _generator.GenerateFileName(metadata, 10, ".jpg");

        Assert.Equal("2022-11-20 18.05.00 - 00010.jpg", fileName);
    }

    [Fact]
    public void GenerateFileName_StripsInvalidCharacters()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2023, 5, 10, 10, 0, 0),
            Make = "Cam:era?",
            Model = "Mod*el/1"
        };

        var fileName = _generator.GenerateFileName(metadata, 1, ".jpg");

        Assert.DoesNotContain(":", fileName);
        Assert.DoesNotContain("?", fileName);
        Assert.DoesNotContain("*", fileName);
        Assert.DoesNotContain("/", fileName);
    }

    [Fact]
    public void GetUniqueFilePath_ResolvesCollisions()
    {
        var metadata = new PhotoMetadata
        {
            DateOriginal = new DateTime(2023, 5, 10, 10, 0, 0),
            Make = "Sony",
            Model = "A7"
        };

        var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(@"C:\test", "2023-05-10 10.00.00 - Sony A7 - 00001.jpg"),
            Path.Combine(@"C:\test", "2023-05-10 10.00.00 - Sony A7 - 00002.jpg")
        };

        var uniquePath = _generator.GetUniqueFilePath(@"C:\test", metadata, ".jpg", file => existingFiles.Contains(file));

        Assert.Equal(Path.Combine(@"C:\test", "2023-05-10 10.00.00 - Sony A7 - 00003.jpg"), uniquePath);
    }
}

