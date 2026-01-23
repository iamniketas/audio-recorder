namespace AudioRecorder.Core.Services;

/// <summary>
/// Состояние воспроизведения
/// </summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Сервис воспроизведения аудиофайлов с поддержкой сегментов
/// </summary>
public interface IAudioPlaybackService : IDisposable
{
    /// <summary>
    /// Загрузить аудиофайл для воспроизведения
    /// </summary>
    Task LoadAsync(string audioPath);

    /// <summary>
    /// Воспроизвести сегмент аудио
    /// </summary>
    /// <param name="start">Начало сегмента</param>
    /// <param name="end">Конец сегмента</param>
    /// <param name="loop">Зациклить воспроизведение</param>
    void PlaySegment(TimeSpan start, TimeSpan end, bool loop = true);

    /// <summary>
    /// Остановить воспроизведение
    /// </summary>
    void Stop();

    /// <summary>
    /// Путь к загруженному файлу
    /// </summary>
    string? LoadedFilePath { get; }

    /// <summary>
    /// Текущее состояние воспроизведения
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Событие изменения состояния
    /// </summary>
    event EventHandler<PlaybackState>? StateChanged;
}
