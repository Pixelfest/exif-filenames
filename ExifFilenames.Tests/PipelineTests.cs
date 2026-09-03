using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Metadata;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Pipeline;
using ExifFilenames.Core.Pipeline.Steps;
using SixLabors.ImageSharp;
using Xunit;

namespace ExifFilenames.Tests;

public class PipelineTests
{
    private class MockMetadataExtractor : IExifMetadataExtractor
    {
        public PhotoMetadata Extract(string filePath)
        {
            return new PhotoMetadata
            {
                DateOriginal = new DateTime(2023, 6, 1, 12, 0, 0),
                Make = "Canon",
                Model = "R5"
            };
        }
    }

    [Fact]
    public async Task Pipeline_CopyFolderAndRename_CopiesFilesWithNewNames()
    {
        var tempSource = Path.Combine(Path.GetTempPath(), "ExifTest_Src_" + Guid.NewGuid().ToString("N"));
        var tempDest = Path.Combine(Path.GetTempPath(), "ExifTest_Dst_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempSource);

            // Create a test file larger than 30KB
            var sampleFile = Path.Combine(tempSource, "IMG_0001.jpg");
            var dummyData = new byte[40 * 1024];
            await File.WriteAllBytesAsync(sampleFile, dummyData);

            var options = new PipelineOptions
            {
                CopyFolder = true,
                DestinationDirectory = tempDest,
                MinFileSizeBytes = 1024
            };

            var context = new PipelineContext(
                tempSource,
                options,
                metadataExtractor: new MockMetadataExtractor());

            var pipeline = PipelineBuilder.CreateDefault().Build();

            await pipeline.ExecuteAsync(context);

            Assert.Equal(1, context.ProcessedCount);
            Assert.True(Directory.Exists(tempDest));

            var expectedFileName = "2023-06-01 12.00.00 - Canon R5 - 00001.jpg";
            var targetFile = Path.Combine(tempDest, expectedFileName);
            Assert.True(File.Exists(targetFile));

            // Original file must remain untouched
            Assert.True(File.Exists(sampleFile));
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true);
        }
    }

    // A sample 3rd step to verify multi-step pipeline extensibility
    private class AddMetadataSidecarStep : IPipelineStep
    {
        public string Name => "Add Metadata Sidecar";

        public Task ExecuteAsync(PipelineContext context, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
        {
            foreach (var file in context.Files.Where(f => f.Status == FileStatus.Processed))
            {
                // Verify the file's current path is the newly copied/renamed file
                var sidecarPath = Path.ChangeExtension(file.CurrentPath, ".txt");
                File.WriteAllText(sidecarPath, $"Metadata for {file.OriginalFileName}");
                file.Properties["SidecarCreated"] = true;
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Pipeline_WithAdditionalSubsequentStep_ProcessesRenamedFiles()
    {
        var tempSource = Path.Combine(Path.GetTempPath(), "ExifTest_Src_" + Guid.NewGuid().ToString("N"));
        var tempDest = Path.Combine(Path.GetTempPath(), "ExifTest_Dst_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempSource);
            var sampleFile = Path.Combine(tempSource, "photo.png");
            await File.WriteAllBytesAsync(sampleFile, new byte[50 * 1024]);

            var options = new PipelineOptions
            {
                CopyFolder = true,
                DestinationDirectory = tempDest,
                MinFileSizeBytes = 1024
            };

            var context = new PipelineContext(tempSource, options, metadataExtractor: new MockMetadataExtractor());

            // Build pipeline with Step 1 (Copy/Structure), Step 2 (EXIF Rename), Step 3 (Sidecar)
            var pipeline = PipelineBuilder.CreateDefault()
                .AddStep<AddMetadataSidecarStep>()
                .Build();

            await pipeline.ExecuteAsync(context);

            Assert.Equal(1, context.ProcessedCount);

            var expectedFileName = "2023-06-01 12.00.00 - Canon R5 - 00001.png";
            var targetFile = Path.Combine(tempDest, expectedFileName);
            var sidecarFile = Path.Combine(tempDest, "2023-06-01 12.00.00 - Canon R5 - 00001.txt");

            Assert.True(File.Exists(targetFile));
            Assert.True(File.Exists(sidecarFile));
            Assert.True(context.Files[0].Properties.ContainsKey("SidecarCreated"));
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true);
        }
    }

    [Fact]
    public async Task Pipeline_ContextMenuPipeline_CopiesRenamesAndResizes()
    {
        var tempSource = Path.Combine(Path.GetTempPath(), "ExifTest_Src_" + Guid.NewGuid().ToString("N"));
        var tempDest = Path.Combine(Path.GetTempPath(), "ExifTest_Dst_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempSource);
            var sampleFile = Path.Combine(tempSource, "IMG_0001.jpg");

            // Create a real JPEG image of 2400x1200
            using (var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(2400, 1200))
            {
                await img.SaveAsJpegAsync(sampleFile);
            }

            var options = new PipelineOptions
            {
                CopyFolder = true,
                DestinationDirectory = tempDest,
                MinFileSizeBytes = 100,
                MaxImageDimension = 1920,
                JpegQuality = 95
            };

            var context = new PipelineContext(tempSource, options, metadataExtractor: new MockMetadataExtractor());

            var pipeline = PipelineBuilder.CreateContextMenu().Build();
            await pipeline.ExecuteAsync(context);

            Assert.Equal(1, context.ProcessedCount);
            Assert.True(Directory.Exists(tempDest));

            var expectedFileName = "2023-06-01 12.00.00 - Canon R5 - 00001.jpg";
            var targetFile = Path.Combine(tempDest, expectedFileName);
            Assert.True(File.Exists(targetFile));

            // Verify original file is still 2400x1200
            var origInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(sampleFile);
            Assert.NotNull(origInfo);
            Assert.Equal(2400, origInfo.Width);
            Assert.Equal(1200, origInfo.Height);

            // Verify resized target file is 1920x960
            var targetInfo = await SixLabors.ImageSharp.Image.IdentifyAsync(targetFile);
            Assert.NotNull(targetInfo);
            Assert.Equal(1920, targetInfo.Width);
            Assert.Equal(960, targetInfo.Height);
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
            if (Directory.Exists(tempDest)) Directory.Delete(tempDest, true);
        }
    }
}

