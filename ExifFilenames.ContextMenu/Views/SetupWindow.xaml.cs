using System;
using System.Windows;
using System.Windows.Media;
using ExifFilenames.ContextMenu.Configuration;
using ExifFilenames.ContextMenu.Registry;

namespace ExifFilenames.ContextMenu.Views;

public partial class SetupWindow : Window
{
    private ContextMenuSettings _settings;

    public SetupWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        LoadValues();
        RefreshStatus();
    }

    private void LoadValues()
    {
        TxtDimension.Text = _settings.MaxImageDimension.ToString();
        TxtQuality.Text = _settings.JpegQuality.ToString();
        TxtSuffix.Text = _settings.TargetFolderSuffix;
        TxtFileSuffix.Text = _settings.TargetFileSuffix;
    }

    private void RefreshStatus()
    {
        bool installed = ContextMenuManager.IsInstalled();
        if (installed)
        {
            TxtStatus.Text = "Geïnstalleerd in Windows Verkenner";
            TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
            BtnInstall.IsEnabled = true;
            BtnUninstall.IsEnabled = true;
        }
        else
        {
            TxtStatus.Text = "Niet geïnstalleerd";
            TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
            BtnInstall.IsEnabled = true;
            BtnUninstall.IsEnabled = false;
        }
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContextMenuManager.Install();
            RefreshStatus();
            MessageBox.Show(
                this,
                "Het contextmenu 'Maak verkleinde versies' is succesvol toegevoegd aan Windows Verkenner!",
                "Installatie voltooid",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Fout bij installeren: {ex.Message}",
                "Fout",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ContextMenuManager.Uninstall();
            RefreshStatus();
            MessageBox.Show(
                this,
                "Het contextmenu is succesvol verwijderd uit Windows Verkenner.",
                "Verwijderd",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Fout bij verwijderen: {ex.Message}",
                "Fout",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtDimension.Text.Trim(), out int dimension) || dimension <= 0)
        {
            MessageBox.Show(this, "Voer een geldige maximale afmeting in (bijv. 1920).", "Ongeldige invoer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtQuality.Text.Trim(), out int quality) || quality is < 1 or > 100)
        {
            MessageBox.Show(this, "Voer een geldige JPEG kwaliteit in tussen 1 en 100.", "Ongeldige invoer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var suffix = ContextMenuSettings.NormalizeSuffix(TxtSuffix.Text, ContextMenuSettings.DefaultTargetFolderSuffix);
        var fileSuffix = ContextMenuSettings.NormalizeSuffix(TxtFileSuffix.Text, ContextMenuSettings.DefaultTargetFileSuffix);

        _settings.MaxImageDimension = dimension;
        _settings.JpegQuality = quality;
        _settings.TargetFolderSuffix = suffix;
        _settings.TargetFileSuffix = fileSuffix;

        try
        {
            SettingsService.Save(_settings);
            MessageBox.Show(this, "De instellingen zijn succesvol opgeslagen.", "Opgeslagen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Fout bij opslaan van instellingen: {ex.Message}", "Fout", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
