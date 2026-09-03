using System;
using System.IO;
using Microsoft.Win32;

namespace ExifFilenames.ContextMenu.Registry;

/// <summary>
/// Beheert de Windows Verkenner contextmenu-registratie in HKCU (Current User).
/// Geen beheerdersrechten (administrator) vereist.
/// </summary>
public static class ContextMenuManager
{
    public const string MenuDisplayName = "Maak verkleinde versies";
    public const string MenuKeyName = "MaakVerkleindeVersies";
    private const string LegacyMenuKeyName = "ExifFilenames";

    private const string DirectoryShellKey = @"Software\Classes\Directory\shell\" + MenuKeyName;
    private const string DirectoryBackgroundShellKey = @"Software\Classes\Directory\Background\shell\" + MenuKeyName;

    private const string LegacyDirectoryShellKey = @"Software\Classes\Directory\shell\" + LegacyMenuKeyName;
    private const string LegacyDirectoryBackgroundShellKey = @"Software\Classes\Directory\Background\shell\" + LegacyMenuKeyName;

    /// <summary>
    /// Controleert of het contextmenu momenteel geregistreerd staat in HKCU.
    /// </summary>
    public static bool IsInstalled()
    {
        using var dirKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DirectoryShellKey);
        if (dirKey != null) return true;

        using var legacyKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(LegacyDirectoryShellKey);
        return legacyKey != null;
    }

    /// <summary>
    /// Registreert het contextmenu voor mappen en achtergrond van mappen.
    /// </summary>
    public static void Install(string? targetExecutablePath = null)
    {
        var exePath = targetExecutablePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            throw new FileNotFoundException($"Uitvoerbaar bestand niet gevonden: {exePath}");
        }

        exePath = Path.GetFullPath(exePath);

        // Verwijder eerst eventuele legacy vermeldingen
        UninstallLegacy();

        // Zoek naar het .ico bestand naast de executable of in de Assets submap
        // Let op: Windows Verkenner verwacht het Icon pad ZONDER aanhalingstekens
        var exeDir = Path.GetDirectoryName(exePath) ?? string.Empty;
        var iconCandidate1 = Path.Combine(exeDir, "Assets", "app.ico");
        var iconCandidate2 = Path.Combine(exeDir, "app.ico");
        string iconValue;

        if (File.Exists(iconCandidate1))
        {
            iconValue = iconCandidate1;
        }
        else if (File.Exists(iconCandidate2))
        {
            iconValue = iconCandidate2;
        }
        else
        {
            iconValue = $"{exePath},0";
        }

        // 1. Registreren voor Directory (rechtermuisknop op map)
        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(DirectoryShellKey))
        {
            key.SetValue("", MenuDisplayName);
            key.SetValue("Icon", iconValue);

            using var cmdKey = key.CreateSubKey("command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        // 2. Registreren voor Directory Background (rechtermuisknop in lege ruimte in map)
        using (var bgKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(DirectoryBackgroundShellKey))
        {
            bgKey.SetValue("", MenuDisplayName);
            bgKey.SetValue("Icon", iconValue);

            using var cmdKey = bgKey.CreateSubKey("command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%V\"");
        }
    }

    /// <summary>
    /// Verwijdert de contextmenu-items uit HKCU.
    /// </summary>
    public static void Uninstall()
    {
        UninstallLegacy();

        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(DirectoryShellKey, throwOnMissingSubKey: false);
        }
        catch { }

        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(DirectoryBackgroundShellKey, throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static void UninstallLegacy()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(LegacyDirectoryShellKey, throwOnMissingSubKey: false);
        }
        catch { }

        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(LegacyDirectoryBackgroundShellKey, throwOnMissingSubKey: false);
        }
        catch { }
    }
}
