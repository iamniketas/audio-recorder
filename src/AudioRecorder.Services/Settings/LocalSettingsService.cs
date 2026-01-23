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
        var appFolder = Path.Combine(appDataPath, "AudioRecorder");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public void SaveSelectedSourceIds(IEnumerable<string> sourceIds)
    {
        try
        {
            var settings = new AppSettings
            {
                SelectedSourceIds = sourceIds.ToList()
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_settingsFilePath, json);
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
            if (!File.Exists(_settingsFilePath))
            {
                return new List<string>();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            return settings?.SelectedSourceIds ?? new List<string>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            return new List<string>();
        }
    }

    private class AppSettings
    {
        public List<string> SelectedSourceIds { get; set; } = new();
    }
}
