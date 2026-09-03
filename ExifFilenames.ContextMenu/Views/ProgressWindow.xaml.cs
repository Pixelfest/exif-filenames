using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shell;
using ExifFilenames.ContextMenu.ViewModels;

namespace ExifFilenames.ContextMenu.Views;

public partial class ProgressWindow : Window
{
    private readonly ProgressViewModel _viewModel;

    public ProgressWindow(ProgressViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartAsync();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProgressViewModel.OverallProgressPercent) ||
            e.PropertyName == nameof(ProgressViewModel.IsRunning) ||
            e.PropertyName == nameof(ProgressViewModel.IsFinished) ||
            e.PropertyName == nameof(ProgressViewModel.HasFailed))
        {
            Dispatcher.Invoke(() =>
            {
                if (TaskbarInfo == null) return;

                if (_viewModel.HasFailed)
                {
                    TaskbarInfo.ProgressState = TaskbarItemProgressState.Error;
                    TaskbarInfo.ProgressValue = 1.0;
                }
                else if (_viewModel.IsFinished)
                {
                    TaskbarInfo.ProgressState = TaskbarItemProgressState.Normal;
                    TaskbarInfo.ProgressValue = 1.0;
                }
                else if (_viewModel.IsRunning)
                {
                    TaskbarInfo.ProgressState = TaskbarItemProgressState.Normal;
                    TaskbarInfo.ProgressValue = Math.Clamp(_viewModel.OverallProgressPercent / 100.0, 0.0, 1.0);
                }
                else
                {
                    TaskbarInfo.ProgressState = TaskbarItemProgressState.None;
                }
            });
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Cancel();
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenDestinationFolder();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsRunning)
        {
            var result = MessageBox.Show(
                this,
                "Het verkleinen van de foto's is nog bezig. Weet u zeker dat u wilt annuleren en afsluiten?",
                "Bewerking afbreken",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _viewModel.Cancel();
        }

        base.OnClosing(e);
    }
}
