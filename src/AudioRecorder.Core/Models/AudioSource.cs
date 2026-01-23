namespace AudioRecorder.Core.Models;

/// <summary>
/// Тип источника аудио
/// </summary>
public enum AudioSourceType
{
    /// <summary>
    /// Системный звук (loopback) - всё, что пользователь слышит
    /// </summary>
    SystemOutput,

    /// <summary>
    /// Микрофон - голос пользователя
    /// </summary>
    Microphone,

    /// <summary>
    /// Импорт файла с диска
    /// </summary>
    FileImport
}

/// <summary>
/// Информация об источнике аудио
/// </summary>
public record AudioSource(
    string Id,
    string Name,
    AudioSourceType Type,
    bool IsDefault = false
);
