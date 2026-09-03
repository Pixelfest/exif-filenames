using System;
using System.Collections.Generic;
using ExifFilenames.Core.Pipeline.Steps;

namespace ExifFilenames.Core.Pipeline;

/// <summary>
/// Fluent builder for composing processing pipelines.
/// </summary>
public class PipelineBuilder
{
    private readonly List<IPipelineStep> _steps = new();

    public PipelineBuilder AddStep(IPipelineStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
        return this;
    }

    public PipelineBuilder AddStep<T>() where T : IPipelineStep, new()
    {
        _steps.Add(new T());
        return this;
    }

    /// <summary>
    /// Creates a standard photo processing pipeline:
    /// Step 1: Discover directory files & initialize folder structure
    /// Step 2: Extract EXIF & rename/copy files
    /// (Additional custom steps can be chained after this)
    /// </summary>
    public static PipelineBuilder CreateDefault()
    {
        return new PipelineBuilder()
            .AddStep<CopyFolderStructureStep>()
            .AddStep<ExifRenameStep>();
    }

    /// <summary>
    /// Creates the context menu processing pipeline:
    /// Step 1: Discover directory files & initialize folder structure
    /// Step 2: Extract EXIF & rename/copy files
    /// Step 3: Resize images to max dimension maintaining ratio and save as 95% JPEG
    /// </summary>
    public static PipelineBuilder CreateContextMenu()
    {
        return new PipelineBuilder()
            .AddStep<CopyFolderStructureStep>()
            .AddStep<ExifRenameStep>()
            .AddStep<ImageResizeStep>();
    }

    public ProcessingPipeline Build()
    {
        return new ProcessingPipeline(_steps);
    }
}

