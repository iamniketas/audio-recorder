using AudioRecorder.Core.Models;
using AudioRecorder.Core.Services;
using AudioRecorder.Services.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;

namespace AudioRecorder.Services.Audio;

/// <summary>
/// Надёжная реализация захвата аудио через WASAPI.
/// Поддерживает множественные источники с правильной синхронизацией.
/// </summary>
public class WasapiAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly object _lock = new();
    private readonly List<IWaveIn> _captures = new();
    private readonly List<BufferedWaveProvider> _buffers = new();
    private WaveFileWriter? _writer;
    private MixingSampleProvider? _mixer;
    private Thread? _recordingThread;
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = new();
    private RecordingInfo _currentInfo;
    private volatile bool _isPaused;

    // Выходной формат: 48kHz, 16-bit, stereo (стандарт для качественного аудио)
    private readonly WaveFormat _outputFormat = new(48000, 16, 2);

    public event EventHandler<RecordingInfo>? RecordingStateChanged;

    public WasapiAudioCaptureService()
    {
        _currentInfo = new RecordingInfo(RecordingState.Stopped, TimeSpan.Zero, 0);
    }

    public Task<IReadOnlyList<AudioSource>> GetAvailableSourcesAsync()
    {
        return Task.Run(() =>
        {
            var sources = new List<AudioSource>();

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                // Получаем устройства вывода (для loopback)
                var defaultRender = GetDefaultDeviceSafe(enumerator, DataFlow.Render, Role.Multimedia);
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    sources.Add(new AudioSource(
                        Id: device.ID,
                        Name: $"🔊 {device.FriendlyName}",
                        Type: AudioSourceType.SystemOutput,
                        IsDefault: defaultRender != null && device.ID == defaultRender.ID
                    ));
                }

                // Получаем устройства ввода (микрофоны)
                var defaultCapture = GetDefaultDeviceSafe(enumerator, DataFlow.Capture, Role.Communications);
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    sources.Add(new AudioSource(
                        Id: device.ID,
                        Name: $"🎤 {device.FriendlyName}",
                        Type: AudioSourceType.Microphone,
                        IsDefault: defaultCapture != null && device.ID == defaultCapture.ID
                    ));
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Ошибка получения устройств: {ex.Message}");
            }

            return (IReadOnlyList<AudioSource>)sources;
        });
    }

    private static MMDevice? GetDefaultDeviceSafe(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        try
        {
            return enumerator.GetDefaultAudioEndpoint(flow, role);
        }
        catch
        {
            return null;
        }
    }

    public Task StartRecordingAsync(IReadOnlyList<AudioSource> sources, string outputPath)
    {
        if (sources.Count == 0)
            throw new ArgumentException("Выберите хотя бы один источник аудио");

        if (_currentInfo.State != RecordingState.Stopped)
            throw new InvalidOperationException("Запись уже идёт");

        return Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    InitializeCapture(sources, outputPath);
                    StartCapture();
                    AppLogger.LogInfo($"Запись начата: {sources.Count} источник(ов) → {outputPath}");
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"Ошибка запуска записи: {ex.Message}");
                    Cleanup();
                    throw;
                }
            }
        });
    }

    private void InitializeCapture(IReadOnlyList<AudioSource> sources, string outputPath)
    {
        using var enumerator = new MMDeviceEnumerator();

        // Формат для внутреннего микширования (float, stereo)
        var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(_outputFormat.SampleRate, 2);
        _mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };

        foreach (var source in sources)
        {
            try
            {
                var device = enumerator.GetDevice(source.Id);
                IWaveIn capture = source.Type == AudioSourceType.SystemOutput
                    ? new WasapiLoopbackCapture(device)
                    : new WasapiCapture(device);

                // Буфер для данных от этого источника
                var buffer = new BufferedWaveProvider(capture.WaveFormat)
                {
                    BufferLength = capture.WaveFormat.AverageBytesPerSecond * 5, // 5 сек буфер
                    DiscardOnBufferOverflow = true,
                    ReadFully = true
                };

                // Конвертируем в нужный формат для микшера
                ISampleProvider sampleProvider = buffer.ToSampleProvider();

                // Ресемплинг если нужен
                if (capture.WaveFormat.SampleRate != _outputFormat.SampleRate)
                {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, _outputFormat.SampleRate);
                }

                // Конвертация каналов
                if (sampleProvider.WaveFormat.Channels == 1)
                {
                    sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
                }
                else if (sampleProvider.WaveFormat.Channels > 2)
                {
                    // Берём только первые 2 канала
                    sampleProvider = new MultiplexingSampleProvider(new[] { sampleProvider }, 2);
                }

                // Добавляем в микшер
                _mixer.AddMixerInput(sampleProvider);

                // Подписываемся на данные
                capture.DataAvailable += (s, e) =>
                {
                    if (e.BytesRecorded > 0 && !_isPaused)
                    {
                        buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                capture.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                    {
                        AppLogger.LogError($"Capture остановлен с ошибкой: {e.Exception.Message}");
                    }
                };

                _captures.Add(capture);
                _buffers.Add(buffer);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"Не удалось инициализировать {source.Name}: {ex.Message}");
            }
        }

        if (_captures.Count == 0)
            throw new InvalidOperationException("Не удалось инициализировать ни один источник");

        // Создаём файл для записи
        _writer = new WaveFileWriter(outputPath, _outputFormat);
    }

    private void StartCapture()
    {
        _cts = new CancellationTokenSource();
        _isPaused = false;
        _stopwatch.Restart();

        // Запускаем поток записи
        _recordingThread = new Thread(RecordingLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "AudioRecordingThread"
        };
        _recordingThread.Start();

        // Запускаем все captures
        foreach (var capture in _captures)
        {
            capture.StartRecording();
        }

        UpdateState(RecordingState.Recording);
    }

    private void RecordingLoop()
    {
        // Размер фрейма: 20ms (хороший баланс между латентностью и CPU)
        const int frameMs = 20;
        int samplesPerFrame = _outputFormat.SampleRate * 2 * frameMs / 1000; // stereo
        var floatBuffer = new float[samplesPerFrame];
        var byteBuffer = new byte[samplesPerFrame * 2]; // 16-bit

        var sw = Stopwatch.StartNew();
        long frameCount = 0;

        while (!_cts!.Token.IsCancellationRequested)
        {
            try
            {
                if (_isPaused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Читаем микшированные данные
                int samplesRead = _mixer!.Read(floatBuffer, 0, samplesPerFrame);

                if (samplesRead > 0 && _writer != null)
                {
                    // Конвертируем float → 16-bit PCM с клиппингом
                    for (int i = 0; i < samplesRead; i++)
                    {
                        float sample = Math.Clamp(floatBuffer[i], -1f, 1f);
                        short pcm = (short)(sample * 32767);
                        byteBuffer[i * 2] = (byte)(pcm & 0xFF);
                        byteBuffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
                    }

                    _writer.Write(byteBuffer, 0, samplesRead * 2);
                }

                // Обновляем UI каждые 500ms
                frameCount++;
                if (frameCount % (500 / frameMs) == 0)
                {
                    UpdateState(RecordingState.Recording);
                }

                // Точный тайминг: спим до следующего фрейма
                long targetMs = frameCount * frameMs;
                long sleepMs = targetMs - sw.ElapsedMilliseconds;
                if (sleepMs > 0)
                {
                    Thread.Sleep((int)sleepMs);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Ошибка в потоке записи: {ex.Message}");
                Thread.Sleep(10);
            }
        }
    }

    public Task StopRecordingAsync()
    {
        if (_currentInfo.State == RecordingState.Stopped)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            lock (_lock)
            {
                AppLogger.LogInfo("Остановка записи...");

                // Сигнал на остановку
                _cts?.Cancel();

                // Останавливаем все captures
                foreach (var capture in _captures)
                {
                    try { capture.StopRecording(); } catch { }
                }

                // Ждём завершения потока записи
                _recordingThread?.Join(2000);

                _stopwatch.Stop();
                Cleanup();
                UpdateState(RecordingState.Stopped);

                AppLogger.LogInfo("Запись остановлена");
            }
        });
    }

    public Task PauseRecordingAsync()
    {
        if (_currentInfo.State != RecordingState.Recording)
            return Task.CompletedTask;

        _isPaused = true;
        _stopwatch.Stop();
        UpdateState(RecordingState.Paused);
        AppLogger.LogInfo("Запись приостановлена");
        return Task.CompletedTask;
    }

    public Task ResumeRecordingAsync()
    {
        if (_currentInfo.State != RecordingState.Paused)
            return Task.CompletedTask;

        _isPaused = false;
        _stopwatch.Start();
        UpdateState(RecordingState.Recording);
        AppLogger.LogInfo("Запись возобновлена");
        return Task.CompletedTask;
    }

    public RecordingInfo GetCurrentRecordingInfo()
    {
        return _currentInfo with
        {
            Duration = _stopwatch.Elapsed,
            FileSizeBytes = _writer?.Length ?? 0
        };
    }

    private void UpdateState(RecordingState state)
    {
        _currentInfo = new RecordingInfo(
            state,
            _stopwatch.Elapsed,
            _writer?.Length ?? 0
        );
        RecordingStateChanged?.Invoke(this, _currentInfo);
    }

    private void Cleanup()
    {
        try { _writer?.Dispose(); } catch { }
        _writer = null;

        foreach (var capture in _captures)
        {
            try { capture.Dispose(); } catch { }
        }
        _captures.Clear();
        _buffers.Clear();

        _mixer = null;
        _cts?.Dispose();
        _cts = null;
        _recordingThread = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
