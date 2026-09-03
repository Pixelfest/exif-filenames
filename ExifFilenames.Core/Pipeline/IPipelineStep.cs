using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Pipeline;

/// <summary>
/// Represents an extensible step within the photo processing pipeline.
/// </summary>
public interface IPipelineStep
{
    /// <summary>
    /// Friendly name of this step for logging and progress reporting.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this step within the given pipeline context.
    /// </summary>
    Task ExecuteAsync(PipelineContext context, IProgress<StepProgress>? progress, CancellationToken cancellationToken);
}

