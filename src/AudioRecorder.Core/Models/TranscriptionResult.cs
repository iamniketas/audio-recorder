namespace AudioRecorder.Core.Models;

/// <summary>
/// Сегмент транскрипции с информацией о спикере
/// </summary>
public record TranscriptionSegment(
    TimeSpan Start,
    TimeSpan End,
    string Speaker,
    string Text
);

/// <summary>
/// Результат транскрипции аудиофайла
/// </summary>
public record TranscriptionResult(
    bool Success,
    string? OutputPath,
    List<TranscriptionSegment> Segments,
    string? ErrorMessage
);

/// <summary>
/// Прогресс транскрипции
/// </summary>
public record TranscriptionProgress(
    TranscriptionState State,
    int ProgressPercent,
    string? StatusMessage
);

/// <summary>
/// Состояние процесса транскрипции
/// </summary>
public enum TranscriptionState
{
    Idle,
    Converting,
    Transcribing,
    Completed,
    Failed
}
