namespace AudioRecorder.Core.Models;

/// <summary>
/// Состояние записи
/// </summary>
public enum RecordingState
{
    /// <summary>
    /// Остановлено
    /// </summary>
    Stopped,

    /// <summary>
    /// Идёт запись
    /// </summary>
    Recording,

    /// <summary>
    /// Пауза
    /// </summary>
    Paused
}

/// <summary>
/// Информация о текущей записи
/// </summary>
public record RecordingInfo(
    RecordingState State,
    TimeSpan Duration,
    long FileSizeBytes,
    AudioSource? Source = null
);
