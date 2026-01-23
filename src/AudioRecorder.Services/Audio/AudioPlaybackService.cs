using AudioRecorder.Core.Services;
using NAudio.Wave;
using PlaybackState = AudioRecorder.Core.Services.PlaybackState;

namespace AudioRecorder.Services.Audio;

/// <summary>
/// Сервис воспроизведения аудио через NAudio
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;
    private System.Timers.Timer? _positionTimer;
    private TimeSpan _segmentStart;
    private TimeSpan _segmentEnd;
    private bool _isLooping;
    private bool _isDisposed;

    public string? LoadedFilePath { get; private set; }
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public event EventHandler<PlaybackState>? StateChanged;

    public Task LoadAsync(string audioPath)
    {
        return Task.Run(() =>
        {
            // Освобождаем предыдущие ресурсы
            CleanupPlayback();

            if (!File.Exists(audioPath))
                throw new FileNotFoundException("Аудиофайл не найден", audioPath);

            _audioReader = new AudioFileReader(audioPath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += OnPlaybackStopped;

            LoadedFilePath = audioPath;

            // Таймер для контроля позиции (50ms)
            _positionTimer = new System.Timers.Timer(50);
            _positionTimer.Elapsed += OnPositionTimerElapsed;
        });
    }

    public void PlaySegment(TimeSpan start, TimeSpan end, bool loop = true)
    {
        if (_audioReader == null || _waveOut == null)
            return;

        _segmentStart = start;
        _segmentEnd = end;
        _isLooping = loop;

        // Устанавливаем позицию на начало сегмента
        _audioReader.CurrentTime = start;

        _waveOut.Play();
        _positionTimer?.Start();

        SetState(PlaybackState.Playing);
    }

    public void Stop()
    {
        _positionTimer?.Stop();
        _waveOut?.Stop();
        _isLooping = false;

        SetState(PlaybackState.Stopped);
    }

    private void OnPositionTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_audioReader == null || _waveOut == null)
            return;

        // Проверяем достижение конца сегмента
        if (_audioReader.CurrentTime >= _segmentEnd)
        {
            if (_isLooping)
            {
                // Возвращаемся к началу сегмента
                _audioReader.CurrentTime = _segmentStart;
            }
            else
            {
                Stop();
            }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!_isLooping)
        {
            _positionTimer?.Stop();
            SetState(PlaybackState.Stopped);
        }
    }

    private void SetState(PlaybackState state)
    {
        if (State != state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }

    private void CleanupPlayback()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _positionTimer = null;

        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        _audioReader?.Dispose();
        _audioReader = null;

        LoadedFilePath = null;
        _isLooping = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        CleanupPlayback();
        GC.SuppressFinalize(this);
    }
}
