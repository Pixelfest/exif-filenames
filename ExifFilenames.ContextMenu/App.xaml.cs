using System;
using System.IO;
using System.Windows;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.ContextMenu.Registry;
using ExifFilenames.ContextMenu.Services;
using ExifFilenames.ContextMenu.ViewModels;
using ExifFilenames.ContextMenu.Views;

namespace ExifFilenames.ContextMenu;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var args = e.Args;

        if (args.Length > 0)
        {
            var firstArg = args[0].Trim('"');
            bool isSilent = Array.Exists(args, a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase));

            if (firstArg.Equals("--install", StringComparison.OrdinalIgnoreCase) ||
                firstArg.Equals("-i", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ContextMenuManager.Install();
                    if (!isSilent)
                    {
                        MessageBox.Show(
                            "Het contextmenu 'Maak verkleinde versies' is succesvol geïnstalleerd in Windows Verkenner.",
                            "Installatie voltooid",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    if (!isSilent)
                    {
                        MessageBox.Show(
                            $"Fout bij installeren van het contextmenu: {ex.Message}",
                            "Fout",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                Shutdown(0);
                return;
            }

            if (firstArg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                firstArg.Equals("-u", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ContextMenuManager.Uninstall();
                    if (!isSilent)
                    {
                        MessageBox.Show(
                            "Het contextmenu 'Maak verkleinde versies' is succesvol verwijderd uit Windows Verkenner.",
                            "Verwijderd",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    if (!isSilent)
                    {
                        MessageBox.Show(
                            $"Fout bij verwijderen van het contextmenu: {ex.Message}",
                            "Fout",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                Shutdown(0);
                return;
            }

            // Wordt aangeroepen vanuit Windows Verkenner met een map!
            if (Directory.Exists(firstArg))
            {
                var settings = SettingsService.Load();
                var viewModel = new ProgressViewModel(firstArg, settings);
                var progressWindow = new ProgressWindow(viewModel);
                MainWindow = progressWindow;
                progressWindow.Show();
                return;
            }

            // Wordt aangeroepen vanuit Windows Verkenner met één of meer geselecteerde bestanden.
            // Verkenner start per bestand een proces; die worden gebundeld tot één venster.
            var filePaths = args.Select(a => a.Trim('"')).Where(File.Exists).ToList();
            if (filePaths.Count > 0)
            {
                var allPaths = await FileSelectionCollector.CollectAsync(filePaths);
                if (allPaths == null)
                {
                    // Paden zijn doorgegeven aan het verzamelende proces.
                    Shutdown(0);
                    return;
                }

                var settings = SettingsService.Load();
                var viewModel = new ProgressViewModel(allPaths, settings);
                var progressWindow = new ProgressWindow(viewModel);
                MainWindow = progressWindow;
                progressWindow.Show();
                return;
            }
        }

        // Geen argumenten (handmatig geopend): Toon beheer en instellingen
        var setupWindow = new SetupWindow();
        MainWindow = setupWindow;
        setupWindow.Show();
    }
}
