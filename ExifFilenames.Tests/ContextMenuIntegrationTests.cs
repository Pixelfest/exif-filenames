using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.ContextMenu.Registry;
using ExifFilenames.ContextMenu.ViewModels;
using ExifFilenames.ContextMenu.Views;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using Xunit;

namespace ExifFilenames.Tests;

public class ContextMenuIntegrationTests
{
    [Fact]
    public void ContextMenuManager_MenuDisplayName_IsDutch()
    {
        Assert.Equal("Maak verkleinde versies", ContextMenuManager.MenuDisplayName);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ContextMenuManager_InstallAndUninstall_WorksProperly()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Use a dummy temp file path for exe
        var dummyExe = Path.Combine(Path.GetTempPath(), "dummy_app.exe");
        File.WriteAllText(dummyExe, "dummy");

        bool wasInstalled = ContextMenuManager.IsInstalled();
        string? previousExe = null;
        if (wasInstalled)
        {
            using var existingKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\" + ContextMenuManager.MenuKeyName + @"\command");
            var cmd = existingKey?.GetValue("")?.ToString();
            if (!string.IsNullOrEmpty(cmd))
            {
                var parts = cmd.Split('"');
                if (parts.Length >= 2) previousExe = parts[1];
            }
        }

        try
        {
            // Install
            ContextMenuManager.Install(dummyExe);
            Assert.True(ContextMenuManager.IsInstalled());

            // Verify registry value
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\" + ContextMenuManager.MenuKeyName))
            {
                Assert.NotNull(key);
                Assert.Equal("Maak verkleinde versies", key.GetValue(""));
                Assert.Equal($"{dummyExe},0", key.GetValue("Icon"));

                using var cmdKey = key.OpenSubKey("command");
                Assert.NotNull(cmdKey);
                Assert.Equal($"\"{dummyExe}\" \"%1\"", cmdKey.GetValue(""));
            }

            // Uninstall
            ContextMenuManager.Uninstall();
            Assert.False(ContextMenuManager.IsInstalled());
        }
        finally
        {
            try { File.Delete(dummyExe); } catch { }
            try
            {
                if (wasInstalled && !string.IsNullOrEmpty(previousExe) && File.Exists(previousExe))
                {
                    ContextMenuManager.Install(previousExe);
                }
            }
            catch { }
        }
    }

    [Fact]
    public void ProgressViewModel_CalculatesNumbersAndPercentagesAccurately()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ExifVMTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var settings = new ContextMenuSettings
            {
                MaxImageDimension = 1920,
                JpegQuality = 90,
                TargetFolderSuffix = " - Verkleind"
            };

            var vm = new ProgressViewModel(tempDir, settings);

            Assert.Equal(1, vm.CurrentStep);
            Assert.True(vm.IsStep1Active);
            Assert.False(vm.IsStep1Completed);
            Assert.StartsWith("Stap 1 van 3", vm.StepTitle);

            // Simulate progress update to step 2 with numbers
            vm.TotalFiles = 100;
            vm.ProcessedFiles = 40;
            vm.RemainingFiles = 60;
            vm.StepProgressPercent = 40;

            Assert.Equal(40, vm.ProcessedFiles);
            Assert.Equal(60, vm.RemainingFiles);
            Assert.Equal(100, vm.TotalFiles);
            Assert.Equal(40, vm.StepProgressPercent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ProgressViewModel_ExecuteWorkflow_ResizesImagesAndCalculatesNumbers()
    {
        var tempSource = Path.Combine(Path.GetTempPath(), "ExifE2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSource);

        try
        {
            // Create 2 test JPEG images
            for (int i = 1; i <= 2; i++)
            {
                var filePath = Path.Combine(tempSource, $"photo_{i}.jpg");
                using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(2000, 1000);
                await img.SaveAsJpegAsync(filePath);
            }

            var settings = new ContextMenuSettings
            {
                MaxImageDimension = 1200,
                JpegQuality = 85,
                TargetFolderSuffix = " - Verkleind"
            };

            var vm = new ProgressViewModel(tempSource, settings);
            await vm.StartAsync();

            Assert.True(vm.IsFinished);
            Assert.False(vm.HasFailed);
            Assert.Equal(2, vm.TotalFiles);
            Assert.Equal(2, vm.ProcessedFiles);
            Assert.Equal(0, vm.RemainingFiles);
            Assert.Equal(100, vm.StepProgressPercent);
            Assert.Equal(100, vm.OverallProgressPercent);
            Assert.True(Directory.Exists(vm.DestinationFolderPath));
            Assert.EndsWith(" - Verkleind", vm.DestinationFolderPath);
        }
        finally
        {
            if (Directory.Exists(tempSource)) Directory.Delete(tempSource, true);
        }
    }

    [Fact]
    public void ComputeUniqueCopyFolderName_EnsuresSpaceBeforeHyphen()
    {
        var source = @"C:\Photos\Vacation";
        var resultWithoutSpace = ExifFilenames.Core.Pipeline.PipelineContext.ComputeUniqueCopyFolderName(source, "- Verkleind");
        Assert.EndsWith(@"Vacation - Verkleind", resultWithoutSpace);

        var resultWithSpace = ExifFilenames.Core.Pipeline.PipelineContext.ComputeUniqueCopyFolderName(source, " - Verkleind");
        Assert.EndsWith(@"Vacation - Verkleind", resultWithSpace);
    }

    [Fact]
    public void SetupWindow_HasSufficientHeightAndConfigurableFields()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = Application.Current ?? new Application();
                var window = new SetupWindow();
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
                Assert.True(window.Height >= 530, "SetupWindow Height should be >= 530");
                Assert.True(window.MinHeight >= 530, "SetupWindow MinHeight should be >= 530");

                var txtDimension = window.FindName("TxtDimension");
                var txtQuality = window.FindName("TxtQuality");
                var txtSuffix = window.FindName("TxtSuffix");

                Assert.NotNull(txtDimension);
                Assert.NotNull(txtQuality);
                Assert.NotNull(txtSuffix);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new Exception("STA test failed", threadEx);
        }
    }
}
