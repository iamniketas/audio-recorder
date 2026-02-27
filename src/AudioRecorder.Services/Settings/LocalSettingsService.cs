using AudioRecorder.Core.Services;
using System.Text.Json;

namespace AudioRecorder.Services.Settings;

/// <summary>
/// Реализация сохранения настроек в локальном файле
/// </summary>
public class LocalSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public LocalSettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "Contora");
        var legacyAppFolder = Path.Combine(appDataPath, "AudioRecorder");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        var legacySettingsPath = Path.Combine(legacyAppFolder, "settings.json");
        if (!File.Exists(_settingsFilePath) && File.Exists(legacySettingsPath))
        {
            File.Copy(legacySettingsPath, _settingsFilePath);
        }
    }

    public void SaveSelectedSourceIds(IEnumerable<string> sourceIds)
    {
        try
        {
            var settings = LoadSettings();
            settings.SelectedSourceIds = sourceIds.ToList();
            SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
        }
    }

    public IReadOnlyList<string> LoadSelectedSourceIds()
    {
        try
        {
            var settings = LoadSettings();
            return settings.SelectedSourceIds;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            return new List<string>();
        }
    }

    public void SaveOutputFolder(string folderPath)
    {
        try
        {
            var settings = LoadSettings();
            settings.OutputFolder = folderPath;
            SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения папки: {ex.Message}");
        }
    }

    public string? LoadOutputFolder()
    {
        try
        {
            var settings = LoadSettings();
            return settings.OutputFolder;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки папки: {ex.Message}");
            return null;
        }
    }

    public void SaveTranscriptionMode(string mode)
    {
        try
        {
            var normalized = string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase)
                ? "light"
                : "quality";

            var settings = LoadSettings();
            settings.TranscriptionMode = normalized;
            SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения режима транскрипции: {ex.Message}");
        }
    }

    public string LoadTranscriptionMode()
    {
        try
        {
            var settings = LoadSettings();
            return string.Equals(settings.TranscriptionMode, "light", StringComparison.OrdinalIgnoreCase)
                ? "light"
                : "quality";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки режима транскрипции: {ex.Message}");
            return "quality";
        }
    }

    private AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return new AppSettings();

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    private void SaveSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_settingsFilePath, json);
    }

    private class AppSettings
    {
        public List<string> SelectedSourceIds { get; set; } = new();
        public string? OutputFolder { get; set; }
        public string TranscriptionMode { get; set; } = "quality";
    }
}
