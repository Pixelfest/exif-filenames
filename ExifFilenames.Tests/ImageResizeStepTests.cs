using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Pipeline;
using ExifFilenames.Core.Pipeline.Steps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ExifFilenames.Tests;

public class ImageResizeStepTests
{
    private static async Task CreateTestImageAsync(string path, int width, int height, Action<Image<Rgba32>>? configure = null)
    {
        using var image = new Image<Rgba32>(width, height);
        // Fill with a simple color pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgba32((byte)(x % 256), (byte)(y % 256), 128, 255);
            }
        }

        configure?.Invoke(image);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await image.SaveAsJpegAsync(path);
    }

    [Fact]
    public async Task ImageResizeStep_DownscalesLandscapeImageTo1920MaxDimension_MaintainingRatio()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFilePath = Path.Combine(tempDir, "landscape.jpg");
            // 2400 x 1200 (ratio 2:1) -> should become 1920 x 960
            await CreateTestImageAsync(testFilePath, 2400, 1200);

            var options = new PipelineOptions
            {
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testFilePath,
                CurrentPath = testFilePath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            var info = await Image.IdentifyAsync(fileItem.CurrentPath);
            Assert.NotNull(info);
            Assert.Equal(1920, info.Width);
            Assert.Equal(960, info.Height);
            Assert.True((bool)fileItem.Properties["Resized"]!);
            Assert.Equal(1920, fileItem.Properties["ImageWidth"]);
            Assert.Equal(960, fileItem.Properties["ImageHeight"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImageResizeStep_DownscalesPortraitImageTo1920MaxDimension_MaintainingRatio()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFilePath = Path.Combine(tempDir, "portrait.jpg");
            // 1200 x 2400 (ratio 1:2) -> should become 960 x 1920
            await CreateTestImageAsync(testFilePath, 1200, 2400);

            var options = new PipelineOptions
            {
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testFilePath,
                CurrentPath = testFilePath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            var info = await Image.IdentifyAsync(fileItem.CurrentPath);
            Assert.NotNull(info);
            Assert.Equal(960, info.Width);
            Assert.Equal(1920, info.Height);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImageResizeStep_DoesNotUpscaleImageSmallerThanMaxDimension()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFilePath = Path.Combine(tempDir, "small.jpg");
            // 800 x 600 -> should remain 800 x 600
            await CreateTestImageAsync(testFilePath, 800, 600);

            var options = new PipelineOptions
            {
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testFilePath,
                CurrentPath = testFilePath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            var info = await Image.IdentifyAsync(fileItem.CurrentPath);
            Assert.NotNull(info);
            Assert.Equal(800, info.Width);
            Assert.Equal(600, info.Height);
            Assert.False((bool)fileItem.Properties["Resized"]!);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImageResizeStep_PreservesExifMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFilePath = Path.Combine(tempDir, "exif_photo.jpg");

            await CreateTestImageAsync(testFilePath, 2500, 1500, img =>
            {
                var exif = new ExifProfile();
                exif.SetValue(ExifTag.Make, "Canon");
                exif.SetValue(ExifTag.Model, "EOS R5");
                img.Metadata.ExifProfile = exif;
            });

            var options = new PipelineOptions
            {
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testFilePath,
                CurrentPath = testFilePath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            // Read the resized image and verify EXIF profile is still present
            using var resizedImage = await Image.LoadAsync(fileItem.CurrentPath);
            Assert.NotNull(resizedImage.Metadata.ExifProfile);

            Assert.True(resizedImage.Metadata.ExifProfile.TryGetValue(ExifTag.Make, out var makeValue));
            Assert.NotNull(makeValue);
            Assert.Equal("Canon", makeValue.Value);

            Assert.True(resizedImage.Metadata.ExifProfile.TryGetValue(ExifTag.Model, out var modelValue));
            Assert.NotNull(modelValue);
            Assert.Equal("EOS R5", modelValue.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImageResizeStep_DryRun_DoesNotModifyFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testFilePath = Path.Combine(tempDir, "test_dryrun.jpg");
            await CreateTestImageAsync(testFilePath, 3000, 2000);

            var lastWrite = File.GetLastWriteTimeUtc(testFilePath);

            var options = new PipelineOptions
            {
                IsDryRun = true,
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testFilePath,
                CurrentPath = testFilePath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            // Verify file was not modified
            var info = await Image.IdentifyAsync(testFilePath);
            Assert.NotNull(info);
            Assert.Equal(3000, info.Width);
            Assert.Equal(2000, info.Height);
            Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(testFilePath));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PipelineBuilder_CreateContextMenu_IncludesResizeStep()
    {
        var builder = PipelineBuilder.CreateContextMenu();
        var pipeline = builder.Build();

        Assert.Equal(3, pipeline.Steps.Count);
        Assert.IsType<CopyFolderStructureStep>(pipeline.Steps[0]);
        Assert.IsType<ExifRenameStep>(pipeline.Steps[1]);
        Assert.IsType<ImageResizeStep>(pipeline.Steps[2]);
    }

    [Fact]
    public void SettingsService_DefaultsTo1920And95()
    {
        var settings = new ContextMenuSettings();
        Assert.Equal(1920, settings.MaxImageDimension);
        Assert.Equal(95, settings.JpegQuality);
        Assert.Equal(PipelineOptions.DefaultMaxImageDimension, settings.MaxImageDimension);
        Assert.Equal(PipelineOptions.DefaultJpegQuality, settings.JpegQuality);
    }

    [Fact]
    public async Task ImageResizeStep_ConvertsPngToJpg_AndRemovesIntermediatePng()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ResizeTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var testPngPath = Path.Combine(tempDir, "photo.png");

            using (var png = new Image<Rgba32>(2000, 1000))
            {
                await png.SaveAsPngAsync(testPngPath);
            }

            var options = new PipelineOptions
            {
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempDir, options);
            var fileItem = new FileItem
            {
                SourcePath = testPngPath,
                CurrentPath = testPngPath,
                TargetPath = testPngPath,
                Status = FileStatus.Processed
            };
            context.Files.Add(fileItem);

            var step = new ImageResizeStep();
            await step.ExecuteAsync(context, null, default);

            // Verify .png was removed and replaced with .jpg
            Assert.False(File.Exists(testPngPath));
            Assert.True(File.Exists(fileItem.CurrentPath));
            Assert.EndsWith(".jpg", fileItem.CurrentPath, StringComparison.OrdinalIgnoreCase);

            var info = await Image.IdentifyAsync(fileItem.CurrentPath);
            Assert.NotNull(info);
            Assert.Equal(1920, info.Width);
            Assert.Equal(960, info.Height);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SettingsService_SaveAndLoad_RoundTripsCustomSettings()
    {
        var tempSettingsFile = SettingsService.GetSettingsFilePath();
        string? backupContent = null;
        if (File.Exists(tempSettingsFile))
        {
            backupContent = File.ReadAllText(tempSettingsFile);
        }

        try
        {
            var custom = new ContextMenuSettings
            {
                MaxImageDimension = 1280,
                JpegQuality = 85
            };
            SettingsService.Save(custom);

            var loaded = SettingsService.Load();
            Assert.Equal(1280, loaded.MaxImageDimension);
            Assert.Equal(85, loaded.JpegQuality);
        }
        finally
        {
            if (backupContent != null)
            {
                File.WriteAllText(tempSettingsFile, backupContent);
            }
            else if (File.Exists(tempSettingsFile))
            {
                File.Delete(tempSettingsFile);
            }
        }
    }
}
