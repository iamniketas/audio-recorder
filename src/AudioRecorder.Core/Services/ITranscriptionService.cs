using AudioRecorder.Core.Models;

namespace AudioRecorder.Core.Services;

/// <summary>
/// Сервис транскрипции аудио
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Расшифровать аудиофайл
    /// </summary>
    /// <param name="audioPath">Путь к аудиофайлу</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Результат транскрипции</returns>
    Task<TranscriptionResult> TranscribeAsync(string audioPath, CancellationToken ct = default);

    /// <summary>
    /// Событие изменения прогресса
    /// </summary>
    event EventHandler<TranscriptionProgress>? ProgressChanged;

    /// <summary>
    /// Доступен ли Whisper
    /// </summary>
    bool IsWhisperAvailable { get; }
}
