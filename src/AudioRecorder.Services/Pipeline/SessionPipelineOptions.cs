using System.Text.Json;

namespace AudioRecorder.Services.Pipeline;

public sealed class SessionPipelineOptions
{
    public string OllamaUrl { get; set; } = "http://localhost:11434/api/generate";
    public string Model { get; set; } = "glm4:latest";
    public int RequestTimeoutSeconds { get; set; } = 600;
    public string MasterFileName { get; set; } = "sessions_master.md";
    public string BackupFileName { get; set; } = "sessions_backup.md";
    public string SystemPromptPath { get; set; } = string.Empty;

    public static string GetConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(localAppData, "Contora");
        Directory.CreateDirectory(appDir);
        return Path.Combine(appDir, "session-pipeline.json");
    }

    public static SessionPipelineOptions LoadOrDefault()
    {
        var configPath = GetConfigPath();
        if (!File.Exists(configPath))
        {
            var defaults = BuildDefaults();
            Save(configPath, defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var parsed = JsonSerializer.Deserialize<SessionPipelineOptions>(json);
            if (parsed == null)
            {
                var defaults = BuildDefaults();
                Save(configPath, defaults);
                return defaults;
            }

            if (string.IsNullOrWhiteSpace(parsed.SystemPromptPath))
            {
                parsed.SystemPromptPath = GetDefaultPromptPath();
            }

            return parsed;
        }
        catch
        {
            var defaults = BuildDefaults();
            Save(configPath, defaults);
            return defaults;
        }
    }

    private static SessionPipelineOptions BuildDefaults()
    {
        var modelOverride = Environment.GetEnvironmentVariable("CONTORA_OLLAMA_MODEL");
        return new SessionPipelineOptions
        {
            Model = string.IsNullOrWhiteSpace(modelOverride) ? "glm4:latest" : modelOverride.Trim(),
            SystemPromptPath = GetDefaultPromptPath()
        };
    }

    private static string GetDefaultPromptPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(localAppData, "Contora");
        Directory.CreateDirectory(appDir);
        return Path.Combine(appDir, "ollama_system_prompt.txt");
    }

    private static void Save(string path, SessionPipelineOptions options)
    {
        var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
