using System;
using System.CommandLine;
using System.IO;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Pipeline;

namespace ExifFilenames
{
    class Program
    {
        static int Main(string[] args)
        {
            Option<DirectoryInfo> dirOption = new("--dir")
            {
                Description = "The path to rename the files in.",
                Required = false
            };

            Option<bool> goOption = new("--go")
            {
                Description = "Whether the files should actually be renamed.",
                Required = false
            };

            Option<bool> recurseOption = new("--recurse")
            {
                Description = "Go through subdirectories as well.",
                Required = false
            };

            Option<bool> unguardedOption = new("--unguarded")
            {
                Description = "Skip the enter key confirmation prompt.",
                Required = false
            };

            Option<bool> copyOption = new("--copy")
            {
                Description = "Create a copy of the folder and process files inside it instead of renaming in-place.",
                Required = false
            };

            Option<DirectoryInfo> targetOption = new("--target")
            {
                Description = "Target directory to copy files to (used with --copy).",
                Required = false
            };

            RootCommand rootCommand = new("Renames files to include date taken and camera details.");
            rootCommand.Options.Add(dirOption);
            rootCommand.Options.Add(goOption);
            rootCommand.Options.Add(recurseOption);
            rootCommand.Options.Add(unguardedOption);
            rootCommand.Options.Add(copyOption);
            rootCommand.Options.Add(targetOption);

            rootCommand.SetAction(parseResult =>
            {
                if (parseResult.Errors.Count > 0)
                {
                    foreach (var parseError in parseResult.Errors)
                    {
                        Log.Error(parseError.Message);
                    }
                    return 1;
                }

                DirectoryInfo directoryInfo = parseResult.GetValue(dirOption) ?? new DirectoryInfo(Environment.CurrentDirectory);
                bool go = parseResult.GetValue(goOption);
                bool recurse = parseResult.GetValue(recurseOption);
                bool unguarded = parseResult.GetValue(unguardedOption);
                bool copy = parseResult.GetValue(copyOption);
                DirectoryInfo? targetDir = parseResult.GetValue(targetOption);

                if (!directoryInfo.Exists)
                {
                    Log.Error($"Directory does not exist: {directoryInfo.FullName}");
                    return 1;
                }

                if (copy)
                {
                    Log.Info("Mode: COPY folder and process files in copy");
                }
                else
                {
                    Log.Info("Mode: IN-PLACE rename");
                }

                if (go)
                {
                    Log.Warning("Files WILL be modified:");
                    Log.Warning($"{directoryInfo.FullName}");
                }
                else
                {
                    Log.Success("TEST RUN (DRY RUN): Files will NOT actually be changed:");
                    Log.Success($"{directoryInfo.FullName}");
                }

                if (recurse)
                {
                    Log.Warning("I will go through subfolders as well.");
                }
                else
                {
                    Log.Success("I'm only doing this folder (no subfolders)");
                }

                if (!unguarded)
                {
                    Log.Info("Press enter to start");
                    var ble = Console.ReadKey();
                    if (ble.Key != ConsoleKey.Enter)
                    {
                        Log.Info("You did not press <Enter> exiting...");
                        return 0;
                    }
                }
                else
                {
                    Log.Info("Skipping confirmation prompt (--unguarded)");
                }

                Log.Info("Here we go");

                var options = new PipelineOptions
                {
                    IsDryRun = !go,
                    Recurse = recurse,
                    CopyFolder = copy,
                    DestinationDirectory = targetDir?.FullName
                };

                var context = new PipelineContext(directoryInfo.FullName, options);

                var progress = new Progress<StepProgress>(p =>
                {
                    switch (p.Severity)
                    {
                        case ProgressSeverity.Trace:
                            Log.Trace(p.Message);
                            break;
                        case ProgressSeverity.Warning:
                            Log.Warning(p.Message);
                            break;
                        case ProgressSeverity.Error:
                            Log.Error(p.Message);
                            break;
                        case ProgressSeverity.Success:
                            Log.Success(p.Message);
                            break;
                        case ProgressSeverity.Info:
                        default:
                            Log.Info(p.Message);
                            break;
                    }
                });

                var pipeline = PipelineBuilder.CreateDefault().Build();

                try
                {
                    pipeline.ExecuteAsync(context, progress).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error($"Execution failed: {ex.Message}");
                    return 1;
                }

                Log.Success($"A total of {context.ProcessedCount} files were processed.");
                Log.Info("Done");
                return 0;
            });

            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }
    }
}

