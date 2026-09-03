namespace ExifFilenames.Core.Models;

public enum ProgressSeverity
{
    Trace,
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Progress reporting message emitted by pipeline steps.
/// </summary>
public record StepProgress(
    string Message,
    ProgressSeverity Severity = ProgressSeverity.Info,
    string? StepName = null,
    int? CurrentIndex = null,
    int? TotalCount = null
);

