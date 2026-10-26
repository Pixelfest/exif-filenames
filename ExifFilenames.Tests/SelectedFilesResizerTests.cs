using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.ContextMenu.ViewModels;
using ExifFilenames.Core.Imaging;
using ExifFilenames.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ExifFilenames.Tests;

public class SelectedFilesResizerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "SelectedFiles_" + Guid.NewGuid().ToString("N"));

    public SelectedFilesResizerTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private async Task<string> CreateJpegWithExifAsync(string fileName, int width, int height)
    {
        var path = Path.Combine(_tempDir, fileName);
        using var image = new Image<Rgba32>(width, height);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Make, "TestCam");
        image.Metadata.ExifProfile.SetValue(ExifTag.DateTimeOriginal, "2024:05:01 12:00:00");
        await image.SaveAsJpegAsync(path);
        return path;
    }

    [Fact]
    public async Task CreatesResizedCopyNextToOriginal_WithSuffixAndWithoutExif()
    {
        var source = await CreateJpegWithExifAsync("IMG_0001.jpg", 3000, 2000);
        var originalBytes = await File.ReadAllBytesAsync(source);

        var resizer = new SelectedFilesResizer(1500, 90, " - verkleind");
        var results = await resizer.ExecuteAsync(new[] { source });

        var expected = Path.Combine(_tempDir, "IMG_0001 - verkleind.jpg");
        var file = Assert.Single(results);
        Assert.Equal(FileStatus.Processed, file.Status);
        Assert.Equal(expected, file.TargetPath);
        Assert.True(File.Exists(expected));

        // Original is untouched
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(source));

        var info = await Image.IdentifyAsync(expected);
        Assert.Equal(1500, info.Width);
        Assert.Equal(1000, info.Height);
        Assert.Null(info.Metadata.ExifProfile);
        Assert.Null(info.Metadata.XmpProfile);
        Assert.Null(info.Metadata.IptcProfile);

        // No temp files left behind
        Assert.Equal(2, Directory.GetFiles(_tempDir).Length);
    }

    [Fact]
    public async Task ProcessesMultipleFiles_AndConvertsPngToJpg()
    {
        var jpg = await CreateJpegWithExifAsync("a.jpg", 800, 600);
        var png = Path.Combine(_tempDir, "b.png");
        using (var image = new Image<Rgba32>(800, 600))
        {
            await image.SaveAsPngAsync(png);
        }

        var resizer = new SelectedFilesResizer(1920, 90, " - verkleind");
        var results = await resizer.ExecuteAsync(new[] { jpg, png });

        Assert.All(results, f => Assert.Equal(FileStatus.Processed, f.Status));
        Assert.True(File.Exists(Path.Combine(_tempDir, "a - verkleind.jpg")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "b - verkleind.jpg")));
    }

    [Fact]
    public async Task DoesNotOverwriteExistingCopy()
    {
        var source = await CreateJpegWithExifAsync("photo.jpg", 400, 300);
        var resizer = new SelectedFilesResizer(1920, 90, " - verkleind");

        await resizer.ExecuteAsync(new[] { source });
        var second = await resizer.ExecuteAsync(new[] { source });

        Assert.Equal(Path.Combine(_tempDir, "photo - verkleind (2).jpg"), second.Single().TargetPath);
        Assert.True(File.Exists(Path.Combine(_tempDir, "photo - verkleind.jpg")));
    }

    [Fact]
    public async Task SkipsUnsupportedAndMissingFiles()
    {
        var text = Path.Combine(_tempDir, "notes.txt");
        await File.WriteAllTextAsync(text, "hello");
        var missing = Path.Combine(_tempDir, "missing.jpg");

        var resizer = new SelectedFilesResizer(1920, 90, " - verkleind");
        var results = await resizer.ExecuteAsync(new[] { text, missing });

        Assert.All(results, f => Assert.Equal(FileStatus.Skipped, f.Status));
        Assert.Single(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public void RejectsEmptySuffix()
    {
        Assert.Throws<ArgumentException>(() => new SelectedFilesResizer(1920, 90, ""));
    }

    [Fact]
    public async Task ProgressViewModel_FileMode_ProcessesSelectedFiles()
    {
        var a = await CreateJpegWithExifAsync("a.jpg", 2000, 1000);
        var b = await CreateJpegWithExifAsync("b.jpg", 2000, 1000);

        var settings = new ContextMenuSettings { MaxImageDimension = 1000, JpegQuality = 85 };
        var vm = new ProgressViewModel(new[] { a, b }, settings);

        Assert.True(vm.IsFileMode);
        Assert.False(vm.ShowSteps);
        Assert.Equal(_tempDir, vm.DestinationFolderPath);

        await vm.StartAsync();

        Assert.True(vm.IsFinished);
        Assert.False(vm.HasFailed);
        Assert.Equal(2, vm.TotalFiles);
        Assert.Equal(2, vm.ProcessedFiles);
        Assert.Equal(100, vm.OverallProgressPercent);
        Assert.True(File.Exists(Path.Combine(_tempDir, "a - verkleind.jpg")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "b - verkleind.jpg")));
    }
}
