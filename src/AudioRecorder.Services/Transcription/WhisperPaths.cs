namespace AudioRecorder.Services.Transcription;

public static class WhisperPaths
{
    public const string EnvWhisperExe = "AUDIORECORDER_WHISPER_EXE";
    public const string EnvWhisperRoot = "AUDIORECORDER_WHISPER_ROOT";
    public const string EnvWhisperModelsRoot = "AUDIORECORDER_WHISPER_MODELS_DIR";
    public const string EnvWhisperModelLargeV2 = "AUDIORECORDER_WHISPER_MODEL_LARGE_V2_DIR";

    private const string RuntimeFolderName = "faster-whisper-xxl";
    private const string ModelDirPrefix = "faster-whisper-";
    private static readonly string[] RequiredModelFiles =
    [
        "config.json",
        "model.bin",
        "tokenizer.json",
        "vocabulary.txt"
    ];

    public static string GetDefaultWhisperPath()
    {
        var envPath = Environment.GetEnvironmentVariable(EnvWhisperExe);
        if (!string.IsNullOrWhiteSpace(envPath))
            return envPath;

        var canonicalPath = GetCanonicalWhisperPath();
        if (File.Exists(canonicalPath))
            return canonicalPath;

        // 1) Production: рядом с приложением.
        var exeDir = AppContext.BaseDirectory;
        var toolsPath = Path.Combine(exeDir, "tools", "faster-whisper-xxl", "faster-whisper-xxl.exe");
        if (File.Exists(toolsPath))
            return toolsPath;

        // 2) Development: в корне репозитория.
        var projectRoot = FindProjectRoot(exeDir);
        if (projectRoot != null)
        {
            toolsPath = Path.Combine(projectRoot, "tools", "faster-whisper-xxl", "faster-whisper-xxl.exe");
            if (File.Exists(toolsPath))
                return toolsPath;
        }

        // 3) Fallback: canonical runtime path in LocalAppData.
        return canonicalPath;
    }

    public static string GetCanonicalRuntimeRoot()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "AudioRecorder", "runtime", RuntimeFolderName);
    }

    public static string GetCanonicalWhisperPath()
    {
        return Path.Combine(GetCanonicalRuntimeRoot(), "faster-whisper-xxl.exe");
    }

    public static string GetModelsRoot(string whisperPath)
    {
        var rootDir = Path.GetDirectoryName(whisperPath) ?? AppContext.BaseDirectory;
        return Path.Combine(rootDir, "_models");
    }

    public static string GetModelDirectory(string whisperPath, string modelName)
    {
        return Path.Combine(GetModelsRoot(whisperPath), $"{ModelDirPrefix}{modelName}");
    }

    public static bool IsModelInstalled(string whisperPath, string modelName)
    {
        var modelDir = GetModelDirectory(whisperPath, modelName);
        if (!Directory.Exists(modelDir))
            return false;

        foreach (var file in RequiredModelFiles)
        {
            if (!File.Exists(Path.Combine(modelDir, file)))
                return false;
        }

        return true;
    }

    public static void RegisterEnvironmentVariables(string whisperPath, string modelName = "large-v2")
    {
        var rootDir = Path.GetDirectoryName(whisperPath) ?? string.Empty;
        var modelsDir = GetModelsRoot(whisperPath);
        var modelDir = GetModelDirectory(whisperPath, modelName);

        SetEnvBothScopes(EnvWhisperExe, whisperPath);
        SetEnvBothScopes(EnvWhisperRoot, rootDir);
        SetEnvBothScopes(EnvWhisperModelsRoot, modelsDir);
        SetEnvBothScopes(EnvWhisperModelLargeV2, modelDir);
    }

    private static void SetEnvBothScopes(string key, string value)
    {
        try
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.User);
        }
        catch
        {
            // Ignore when user-level env vars are not writable.
        }
    }

    private static string? FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AudioRecorder.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}
