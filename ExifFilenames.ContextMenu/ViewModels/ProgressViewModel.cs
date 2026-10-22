using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.Core.Imaging;
using ExifFilenames.Core.Models;
using ExifFilenames.Core.Pipeline;

namespace ExifFilenames.ContextMenu.ViewModels;

public class ProgressViewModel : INotifyPropertyChanged
{
    private readonly string _sourcePath;
    private readonly IReadOnlyList<string>? _filePaths;
    private readonly ContextMenuSettings _settings;
    private CancellationTokenSource? _cts;

    private int _currentStep = 1;
    private string _stepTitle = "Stap 1 van 3: Bestanden zoeken";
    private string _stepDetail = "Bezig met tellen...";
    private int _stepProgressPercent = 0;
    private int _overallProgressPercent = 0;

    private int _totalFiles = 0;
    private int _processedFiles = 0;
    private int _remainingFiles = 0;
    private int _skippedFiles = 0;
    private int _failedFiles = 0;

    private bool _isStep1Active = true;
    private bool _isStep1Completed = false;
    private bool _isStep2Active = false;
    private bool _isStep2Completed = false;
    private bool _isStep3Active = false;
    private bool _isStep3Completed = false;

    private bool _isRunning = true;
    private bool _isFinished = false;
    private bool _hasFailed = false;
    private string? _errorMessage;
    private string _destinationDirectory = string.Empty;

    public ProgressViewModel(string sourcePath, ContextMenuSettings settings)
    {
        _sourcePath = Path.GetFullPath(sourcePath);
        _settings = settings;
    }

    /// <summary>
    /// File mode: makes a resized, metadata-free copy next to each selected file.
    /// </summary>
    public ProgressViewModel(IReadOnlyList<string> filePaths, ContextMenuSettings settings)
    {
        if (filePaths.Count == 0)
            throw new ArgumentException("At least one file is required.", nameof(filePaths));

        _filePaths = filePaths;
        _sourcePath = Path.GetDirectoryName(Path.GetFullPath(filePaths[0])) ?? string.Empty;
        _settings = settings;
        _destinationDirectory = _sourcePath;
        _stepTitle = "Foto's verkleinen en metadata verwijderen";
        _stepDetail = $"0 van {filePaths.Count} foto's (0%)";
        _totalFiles = filePaths.Count;
        _remainingFiles = filePaths.Count;
    }

    public bool IsFileMode => _filePaths != null;
    public bool ShowSteps => !IsFileMode;

    public string SourceDescription => _filePaths switch
    {
        null => $"Bronmap: {SourceFolderName}",
        { Count: 1 } => $"Bestand: {Path.GetFileName(_filePaths[0])}",
        _ => $"{_filePaths.Count} geselecteerde bestanden in {SourceFolderName}"
    };

    public string SummaryTotalLabel => IsFileMode ? "Totaal geselecteerd:" : "Totaal gescand:";
    public string SummarySkippedLabel => IsFileMode ? "Overgeslagen (geen ondersteunde foto's):" : "Overgeslagen (geen foto's of te klein):";
    public string DestinationLabel => IsFileMode ? "Opgeslagen naast de originelen (zonder EXIF-gegevens):" : "Opgeslagen in doelmap:";

    public string SourceFolderName => Path.GetFileName(_sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public string SourceFolderPath => _sourcePath;

    public string DestinationFolderName => string.IsNullOrWhiteSpace(_destinationDirectory)
        ? string.Empty
        : Path.GetFileName(_destinationDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public string DestinationFolderPath
    {
        get => _destinationDirectory;
        set { _destinationDirectory = value; OnPropertyChanged(); OnPropertyChanged(nameof(DestinationFolderName)); }
    }

    public int CurrentStep
    {
        get => _currentStep;
        set { _currentStep = value; OnPropertyChanged(); }
    }

    public string StepTitle
    {
        get => _stepTitle;
        set { _stepTitle = value; OnPropertyChanged(); }
    }

    public string StepDetail
    {
        get => _stepDetail;
        set { _stepDetail = value; OnPropertyChanged(); }
    }

    public int StepProgressPercent
    {
        get => _stepProgressPercent;
        set { _stepProgressPercent = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
    }

    public int OverallProgressPercent
    {
        get => _overallProgressPercent;
        set { _overallProgressPercent = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
    }

    public int TotalFiles
    {
        get => _totalFiles;
        set { _totalFiles = value; OnPropertyChanged(); }
    }

    public int ProcessedFiles
    {
        get => _processedFiles;
        set { _processedFiles = value; OnPropertyChanged(); }
    }

    public int RemainingFiles
    {
        get => _remainingFiles;
        set { _remainingFiles = value; OnPropertyChanged(); }
    }

    public int SkippedFiles
    {
        get => _skippedFiles;
        set { _skippedFiles = value; OnPropertyChanged(); }
    }

    public int FailedFiles
    {
        get => _failedFiles;
        set { _failedFiles = value; OnPropertyChanged(); }
    }

    public bool IsStep1Active
    {
        get => _isStep1Active;
        set { _isStep1Active = value; OnPropertyChanged(); }
    }

    public bool IsStep1Completed
    {
        get => _isStep1Completed;
        set { _isStep1Completed = value; OnPropertyChanged(); }
    }

    public bool IsStep2Active
    {
        get => _isStep2Active;
        set { _isStep2Active = value; OnPropertyChanged(); }
    }

    public bool IsStep2Completed
    {
        get => _isStep2Completed;
        set { _isStep2Completed = value; OnPropertyChanged(); }
    }

    public bool IsStep3Active
    {
        get => _isStep3Active;
        set { _isStep3Active = value; OnPropertyChanged(); }
    }

    public bool IsStep3Completed
    {
        get => _isStep3Completed;
        set { _isStep3Completed = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); }
    }

    public bool IsFinished
    {
        get => _isFinished;
        set { _isFinished = value; OnPropertyChanged(); }
    }

    public bool HasFailed
    {
        get => _hasFailed;
        set { _hasFailed = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;

        try
        {
            var (total, processed, skipped, failed) = IsFileMode
                ? await RunFilesAsync(cancellationToken)
                : await RunFolderAsync(cancellationToken);

            TotalFiles = total;
            ProcessedFiles = processed;
            SkippedFiles = skipped;
            FailedFiles = failed;
            RemainingFiles = 0;

            IsStep1Completed = true;
            IsStep2Completed = true;
            IsStep3Completed = true;
            IsStep1Active = false;
            IsStep2Active = false;
            IsStep3Active = false;

            StepProgressPercent = 100;
            OverallProgressPercent = 100;
            StepTitle = "Voltooid";
            StepDetail = $"{ProcessedFiles} van {TotalFiles} foto's gereed (100%)";

            IsRunning = false;
            IsFinished = true;
        }
        catch (OperationCanceledException)
        {
            IsRunning = false;
            HasFailed = true;
            ErrorMessage = "De bewerking is geannuleerd.";
            StepDetail = "Geannuleerd door gebruiker.";
        }
        catch (Exception ex)
        {
            IsRunning = false;
            HasFailed = true;
            ErrorMessage = $"Er is een fout opgetreden: {ex.Message}";
            StepDetail = "Mislukt.";
        }
    }

    private async Task<(int Total, int Processed, int Skipped, int Failed)> RunFolderAsync(CancellationToken cancellationToken)
    {
        var options = new PipelineOptions
        {
            CopyFolder = true,
            Recurse = true,
            IsDryRun = false,
            TargetFolderSuffix = _settings.TargetFolderSuffix,
            MaxImageDimension = _settings.MaxImageDimension,
            JpegQuality = _settings.JpegQuality
        };

        var context = new PipelineContext(_sourcePath, options);
        DestinationFolderPath = context.DestinationDirectory;

        var progress = new Progress<StepProgress>(HandleProgress);
        var pipeline = PipelineBuilder.CreateContextMenu().Build();

        await Task.Run(async () =>
        {
            await pipeline.ExecuteAsync(context, progress, cancellationToken);
        }, cancellationToken);

        return (context.TotalCount, context.ProcessedCount, context.SkippedCount, context.FailedCount);
    }

    private async Task<(int Total, int Processed, int Skipped, int Failed)> RunFilesAsync(CancellationToken cancellationToken)
    {
        var resizer = new SelectedFilesResizer(_settings.MaxImageDimension, _settings.JpegQuality, _settings.TargetFileSuffix);
        var progress = new Progress<StepProgress>(HandleProgress);

        var files = await Task.Run(() => resizer.ExecuteAsync(_filePaths!, progress, cancellationToken), cancellationToken);

        return (
            files.Count,
            files.Count(f => f.Status == FileStatus.Processed),
            files.Count(f => f.Status == FileStatus.Skipped),
            files.Count(f => f.Status == FileStatus.Failed));
    }

    public void Cancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    public void OpenDestinationFolder()
    {
        if (!string.IsNullOrWhiteSpace(DestinationFolderPath) && Directory.Exists(DestinationFolderPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DestinationFolderPath,
                UseShellExecute = true
            });
        }
    }

    private void HandleProgress(StepProgress p)
    {
        if (p.StepName == SelectedFilesResizer.StepName)
        {
            if (p.CurrentIndex.HasValue && p.TotalCount.HasValue)
            {
                int curr = p.CurrentIndex.Value;
                int tot = p.TotalCount.Value;
                ProcessedFiles = curr;
                RemainingFiles = Math.Max(0, tot - curr);

                StepProgressPercent = (int)Math.Round((double)curr / Math.Max(1, tot) * 100);
                StepDetail = $"{curr} van {tot} foto's ({StepProgressPercent}%)";
                OverallProgressPercent = StepProgressPercent;
            }
        }
        else if (p.StepName != null)
        {
            if (p.StepName.Contains("Prepare", StringComparison.OrdinalIgnoreCase))
            {
                CurrentStep = 1;
                IsStep1Active = true;
                IsStep2Active = false;
                IsStep3Active = false;
                StepTitle = "Stap 1 van 3: Mappen voorbereiden & bestanden zoeken";

                if (p.CurrentIndex.HasValue && p.TotalCount.HasValue)
                {
                    TotalFiles = p.TotalCount.Value;
                    StepProgressPercent = (int)Math.Round((double)p.CurrentIndex.Value / Math.Max(1, p.TotalCount.Value) * 100);
                    StepDetail = $"{p.CurrentIndex.Value} van {p.TotalCount.Value} ({StepProgressPercent}%)";
                }
                else
                {
                    StepProgressPercent = 100;
                    StepDetail = "Bestanden inventariseren...";
                }

                OverallProgressPercent = Math.Min(10, (int)(StepProgressPercent * 0.10));
            }
            else if (p.StepName.Contains("EXIF", StringComparison.OrdinalIgnoreCase) ||
                     p.StepName.Contains("Rename", StringComparison.OrdinalIgnoreCase))
            {
                CurrentStep = 2;
                IsStep1Completed = true;
                IsStep1Active = false;
                IsStep2Active = true;
                IsStep3Active = false;
                StepTitle = "Stap 2 van 3: Foto's ordenen en hernoemen op datum";

                if (p.CurrentIndex.HasValue && p.TotalCount.HasValue)
                {
                    int curr = p.CurrentIndex.Value;
                    int tot = p.TotalCount.Value;
                    TotalFiles = tot;
                    ProcessedFiles = curr;
                    RemainingFiles = Math.Max(0, tot - curr);

                    StepProgressPercent = (int)Math.Round((double)curr / Math.Max(1, tot) * 100);
                    StepDetail = $"{curr} van {tot} foto's ({StepProgressPercent}%)";
                    OverallProgressPercent = 10 + (int)(StepProgressPercent * 0.45);
                }
            }
            else if (p.StepName.Contains("Resize", StringComparison.OrdinalIgnoreCase))
            {
                CurrentStep = 3;
                IsStep1Completed = true;
                IsStep2Completed = true;
                IsStep1Active = false;
                IsStep2Active = false;
                IsStep3Active = true;
                StepTitle = "Stap 3 van 3: Foto's verkleinen";

                if (p.CurrentIndex.HasValue && p.TotalCount.HasValue)
                {
                    int curr = p.CurrentIndex.Value;
                    int tot = p.TotalCount.Value;
                    ProcessedFiles = curr;
                    RemainingFiles = Math.Max(0, tot - curr);

                    StepProgressPercent = (int)Math.Round((double)curr / Math.Max(1, tot) * 100);
                    StepDetail = $"{curr} van {tot} foto's verkleind ({StepProgressPercent}%)";
                    OverallProgressPercent = 55 + (int)(StepProgressPercent * 0.45);
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
