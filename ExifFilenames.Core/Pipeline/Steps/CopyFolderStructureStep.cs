using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.Core.Models;

namespace ExifFilenames.Core.Pipeline.Steps;

/// <summary>
/// Pipeline step that discovers input files and, if folder copying is enabled,
/// initializes the target directory structure.
/// </summary>
public class CopyFolderStructureStep : IPipelineStep
{
    public string Name => "Prepare Directory & Discover Files";

    public Task ExecuteAsync(PipelineContext context, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(context.SourceDirectory))
        {
            var err = $"Source directory does not exist: {context.SourceDirectory}";
            progress?.Report(new StepProgress(err, ProgressSeverity.Error, Name));
            throw new DirectoryNotFoundException(err);
        }

        var searchOption = context.Options.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        progress?.Report(new StepProgress(
            $"Discovering files in '{context.SourceDirectory}' (Recurse: {context.Options.Recurse})...",
            ProgressSeverity.Info,
            Name));

        var discoveredPaths = Directory.GetFiles(context.SourceDirectory, "*", searchOption);
        foreach (var path in discoveredPaths)
        {
            context.Files.Add(new FileItem
            {
                SourcePath = path,
                CurrentPath = path
            });
        }

        progress?.Report(new StepProgress(
            $"Discovered {context.Files.Count} files in source directory.",
            ProgressSeverity.Info,
            Name));

        if (context.Options.CopyFolder)
        {
            progress?.Report(new StepProgress(
                $"Target directory will be: '{context.DestinationDirectory}'",
                ProgressSeverity.Info,
                Name));

            if (!context.Options.IsDryRun)
            {
                Directory.CreateDirectory(context.DestinationDirectory);

                // Mirror subdirectories if recurring
                if (context.Options.Recurse)
                {
                    foreach (var dir in Directory.GetDirectories(context.SourceDirectory, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rel = Path.GetRelativePath(context.SourceDirectory, dir);
                        var targetSubdir = Path.Combine(context.DestinationDirectory, rel);
                        Directory.CreateDirectory(targetSubdir);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}

