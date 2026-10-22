using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExifFilenames.Core.Models;

namespace ExifFilenames.ContextMenu.Configuration;

/// <summary>
/// Service to load and save context menu configuration next to the executable.
/// </summary>
public static class SettingsService
{
    public const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string GetSettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
    }

    /// <summary>
    /// Loads settings from settings.json next to the executable.
    /// If the file does not exist, a default settings.json file is created and returned.
    /// Invalid values automatically fall back to standard defaults.
    /// </summary>
    public static ContextMenuSettings Load()
    {
        var path = GetSettingsFilePath();

        if (!File.Exists(path))
        {
            var defaultSettings = new ContextMenuSettings();
            try
            {
                Save(defaultSettings);
            }
            catch
            {
                // If directory is read-only, return defaults without failing
            }
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<ContextMenuSettings>(json, JsonOptions) ?? new ContextMenuSettings();

            // Validate and sanitize values
            if (settings.MaxImageDimension <= 0)
            {
                settings.MaxImageDimension = PipelineOptions.DefaultMaxImageDimension;
            }

            if (settings.JpegQuality is < 1 or > 100)
            {
                settings.JpegQuality = PipelineOptions.DefaultJpegQuality;
            }

            settings.TargetFolderSuffix = ContextMenuSettings.NormalizeSuffix(
                settings.TargetFolderSuffix, ContextMenuSettings.DefaultTargetFolderSuffix);
            settings.TargetFileSuffix = ContextMenuSettings.NormalizeSuffix(
                settings.TargetFileSuffix, ContextMenuSettings.DefaultTargetFileSuffix);

            return settings;
        }
        catch
        {
            return new ContextMenuSettings();
        }
    }

    /// <summary>
    /// Saves settings to settings.json next to the executable.
    /// </summary>
    public static void Save(ContextMenuSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var path = GetSettingsFilePath();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}

