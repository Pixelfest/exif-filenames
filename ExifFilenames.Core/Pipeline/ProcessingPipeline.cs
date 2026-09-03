using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Pipeline;

/// <summary>
/// Orchestrates the sequential execution of registered pipeline steps.
/// </summary>
public class ProcessingPipeline
{
    private readonly List<IPipelineStep> _steps;

    public IReadOnlyList<IPipelineStep> Steps => _steps.AsReadOnly();

    public ProcessingPipeline(IEnumerable<IPipelineStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = new List<IPipelineStep>(steps);
    }

    /// <summary>
    /// Executes all steps in the pipeline in order.
    /// </summary>
    public async Task<PipelineContext> ExecuteAsync(
        PipelineContext context,
        IProgress<StepProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        progress?.Report(new StepProgress(
            $"Starting pipeline with {_steps.Count} step(s).",
            ProgressSeverity.Info));

        for (int i = 0; i < _steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = _steps[i];
            progress?.Report(new StepProgress(
                $"--- Executing step [{i + 1}/{_steps.Count}]: {step.Name} ---",
                ProgressSeverity.Info,
                step.Name));

            await step.ExecuteAsync(context, progress, cancellationToken);
        }

        progress?.Report(new StepProgress(
            $"Pipeline completed. Total: {context.TotalCount}, Processed: {context.ProcessedCount}, Skipped: {context.SkippedCount}, Failed: {context.FailedCount}.",
            ProgressSeverity.Success));

        return context;
    }
}

