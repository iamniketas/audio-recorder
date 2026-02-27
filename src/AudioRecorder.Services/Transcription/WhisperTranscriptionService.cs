using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AudioRecorder.Core.Models;
using AudioRecorder.Core.Services;
using AudioRecorder.Services.Audio;

namespace AudioRecorder.Services.Transcription;

/// <summary>
/// Сервис транскрипции через faster-whisper-xxl
/// </summary>
public partial class WhisperTranscriptionService : ITranscriptionService
{
    private readonly string _whisperPath;
    private readonly string _modelName;
    private readonly bool _enableDiarization;
    private TimeSpan _audioDuration;
    private DateTime _transcriptionStartTime;

    public event EventHandler<TranscriptionProgress>? ProgressChanged;

    public bool IsWhisperAvailable => File.Exists(_whisperPath);

    public WhisperTranscriptionService(
        string? whisperPath = null,
        string modelName = "large-v2",
        bool enableDiarization = true)
    {
        _whisperPath = whisperPath ?? WhisperPaths.GetDefaultWhisperPath();
        _modelName = modelName;
        _enableDiarization = enableDiarization;
    }
    public async Task<TranscriptionResult> TranscribeAsync(string audioPath, CancellationToken ct = default)
    {
        if (!File.Exists(audioPath))
        {
            return new TranscriptionResult(false, null, [], $"Файл не найден: {audioPath}");
        }

        if (!IsWhisperAvailable)
        {
            return new TranscriptionResult(false, null, [],
                $"Whisper не найден. Скачайте faster-whisper-xxl и поместите в:\n{_whisperPath}");
        }

        string? tempFilePath = null;
        string? tempOutputDir = null;

        // Сохраняем оригинальное имя файла для результата
        var originalFileName = Path.GetFileNameWithoutExtension(audioPath);
        var originalDir = Path.GetDirectoryName(audioPath) ?? Path.GetTempPath();

        try
        {
            var processPath = audioPath;

            // Копируем файл в TEMP если путь содержит не-ASCII символы
            if (ContainsNonAscii(audioPath))
            {
                RaiseProgress(TranscriptionState.Converting, 0, "Копирование файла...");
                tempOutputDir = Path.Combine(Path.GetTempPath(), $"Contora_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempOutputDir);

                var safeFileName = $"audio_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(audioPath)}";
                tempFilePath = Path.Combine(tempOutputDir, safeFileName);
                File.Copy(audioPath, tempFilePath);
                processPath = tempFilePath;
            }

            // Конвертируем WAV в MP3 если нужно
            if (AudioConverter.IsWavFile(processPath))
            {
                RaiseProgress(TranscriptionState.Converting, 0, "Конвертация в MP3...");
                var mp3Path = await AudioConverter.ConvertToMp3Async(processPath, deleteOriginal: tempFilePath != null);
                processPath = mp3Path;
                if (tempFilePath != null)
                    tempFilePath = mp3Path;
                RaiseProgress(TranscriptionState.Converting, 100, "Конвертация завершена");
            }

            // Получаем длительность аудио
            _audioDuration = await GetAudioDurationAsync(processPath);
            _transcriptionStartTime = DateTime.Now;

            // Запускаем транскрипцию
            RaiseProgress(TranscriptionState.Transcribing, 0, "Запуск Whisper...");

            var outputDir = tempOutputDir ?? originalDir;
            var result = await RunWhisperAsync(processPath, outputDir, originalFileName, originalDir, ct);

            // Копируем результат обратно если работали в TEMP
            if (result.Success && tempOutputDir != null && result.OutputPath != null)
            {
                var finalTxtPath = Path.Combine(originalDir, $"{originalFileName}.txt");
                File.Copy(result.OutputPath, finalTxtPath, overwrite: true);
                result = result with { OutputPath = finalTxtPath };
            }

            if (result.Success)
            {
                RaiseProgress(TranscriptionState.Completed, 100, "Транскрипция завершена");
            }
            else
            {
                RaiseProgress(TranscriptionState.Failed, 0, result.ErrorMessage);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            RaiseProgress(TranscriptionState.Failed, 0, "Транскрипция отменена");
            return new TranscriptionResult(false, null, [], "Отменено пользователем");
        }
        catch (Exception ex)
        {
            RaiseProgress(TranscriptionState.Failed, 0, ex.Message);
            return new TranscriptionResult(false, null, [], ex.Message);
        }
        finally
        {
            // Очищаем временные файлы
            if (tempOutputDir != null)
            {
                try { Directory.Delete(tempOutputDir, recursive: true); } catch { }
            }
        }
    }

    private static bool ContainsNonAscii(string path)
    {
        return path.Any(c => c > 127);
    }

    private static async Task<TimeSpan> GetAudioDurationAsync(string audioPath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var reader = new NAudio.Wave.AudioFileReader(audioPath);
                return reader.TotalTime;
            });
        }
        catch
        {
            return TimeSpan.FromMinutes(5); // Fallback
        }
    }

    private async Task<TranscriptionResult> RunWhisperAsync(string audioPath, string outputDir, string outputFileName, string logDir, CancellationToken ct)
    {
        var arguments = BuildArguments(audioPath, outputDir);

        // Создаём лог-файл для полного вывода Whisper в оригинальной директории (НЕ в TEMP)
        var logPath = Path.Combine(logDir, $"{outputFileName}_whisper.log");
        StreamWriter? logWriter = null;

        try
        {
            logWriter = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };
            logWriter.WriteLine($"=== Whisper Transcription Log ===");
            logWriter.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logWriter.WriteLine($"Command: \"{_whisperPath}\" {arguments}");
            logWriter.WriteLine($"Audio: {audioPath}");
            logWriter.WriteLine($"Output: {outputDir}");
            logWriter.WriteLine($"=================================\n");
        }
        catch
        {
            // Если не удалось создать лог - продолжаем без него
        }

        var psi = new ProcessStartInfo
        {
            FileName = _whisperPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        // Для длинных файлов не накапливаем весь вывод — только последние строки для ошибок
        var errorLines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        const int MaxErrorLines = 200; // Увеличил до 200 для лучшей диагностики

        var outputComplete = new TaskCompletionSource<bool>();
        var errorComplete = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                outputComplete.TrySetResult(true);
                return;
            }

            // Логируем в файл
            try { logWriter?.WriteLine($"[OUT] {e.Data}"); } catch { }

            ParseProgressFromOutput(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                errorComplete.TrySetResult(true);
                return;
            }

            // Логируем в файл
            try { logWriter?.WriteLine($"[ERR] {e.Data}"); } catch { }

            // Сохраняем только последние строки для error message
            errorLines.Enqueue(e.Data);
            while (errorLines.Count > MaxErrorLines)
            {
                errorLines.TryDequeue(out string? _);
            }

            // faster-whisper-xxl выводит прогресс в stderr
            ParseProgressFromOutput(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Ждём завершения процесса
        await process.WaitForExitAsync(ct);

        // КРИТИЧНО: дожидаемся завершения чтения output/error streams
        // WaitForExitAsync может вернуть управление раньше, чем обработаются все данные
        await Task.WhenAll(outputComplete.Task, errorComplete.Task).ConfigureAwait(false);

        // Закрываем лог
        try
        {
            logWriter?.WriteLine($"\n=================================");
            logWriter?.WriteLine($"Exit Code: {process.ExitCode}");
            logWriter?.WriteLine($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logWriter?.Dispose();
        }
        catch { }

        // КРИТИЧНО: Whisper может крашиться с кодом -1073740791 при cleanup
        // ПОСЛЕ успешной записи результата. Проверяем наличие файла ПЕРЕД проверкой exit code.
        var whisperBaseName = Path.GetFileNameWithoutExtension(audioPath);
        var whisperTxtPath = Path.Combine(outputDir, $"{whisperBaseName}.txt");
        bool resultFileExists = File.Exists(whisperTxtPath);

        if (process.ExitCode != 0)
        {
            // Код -1073740791 (0xC0000409) - краш при cleanup pyannote/CUDA
            // Если файл создан - игнорируем ошибку (транскрипция завершена успешно)
            if (process.ExitCode == -1073740791 && resultFileExists)
            {
                // Транскрипция успешна, несмотря на краш при cleanup
                System.Diagnostics.Debug.WriteLine($"[Whisper] Ignoring cleanup crash -1073740791, result file exists");
            }
            else
            {
                var errorMessage = string.Join(Environment.NewLine, errorLines);
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = $"Whisper завершился с кодом {process.ExitCode}";

                // Добавляем путь к логу в сообщение об ошибке
                errorMessage += $"\n\nПолный лог сохранён в:\n{logPath}";

                return new TranscriptionResult(false, null, [], errorMessage);
            }
        }

        // whisperBaseName и whisperTxtPath уже определены выше для проверки exit code
        if (!resultFileExists)
        {
            return new TranscriptionResult(false, null, [], "Файл транскрипции не создан");
        }

        // Переименовываем файл в соответствии с оригинальным именем
        var finalTxtPath = Path.Combine(outputDir, $"{outputFileName}.txt");
        if (whisperTxtPath != finalTxtPath)
        {
            if (File.Exists(finalTxtPath))
                File.Delete(finalTxtPath);
            File.Move(whisperTxtPath, finalTxtPath);
        }

        // Парсим результат
        var segments = await ParseTranscriptionFileAsync(finalTxtPath);

        return new TranscriptionResult(true, finalTxtPath, segments, null);
    }

    private string BuildArguments(string audioPath, string outputDir)
    {
        // Используем те же параметры, что работают у пользователя:
        // faster-whisper-xxl -pp -o source --standard -f txt -m large-v2 --diarize pyannote_v3.1
        // -pp = print_progress — обновления прогресса каждую секунду
        // --standard — стандартный формат вывода (более стабильный)
        var sb = new StringBuilder();
        sb.Append("-pp ");                       // Явный вывод прогресса
        sb.Append($"-o \"{outputDir}\" ");
        sb.Append("--standard ");                 // Стандартный формат
        sb.Append("-f txt ");
        sb.Append($"-m {_modelName} ");
        if (_enableDiarization)
        {
            sb.Append("--diarize pyannote_v3.1 ");
        }
        sb.Append($"\"{audioPath}\"");
        return sb.ToString();
    }

    private void ParseProgressFromOutput(string line)
    {
        // faster-whisper-xxl с флагом -pp выводит прогресс в формате:
        // "  1% |   35/4423 | 00:01<<02:22 | 30.73 audio seconds/s"
        // Процент | Сегменты | Время(прошло<<осталось) | Скорость

        var elapsed = DateTime.Now - _transcriptionStartTime;

        // Парсим полный формат прогресса
        var fullProgressMatch = FullProgressRegex().Match(line);
        if (fullProgressMatch.Success)
        {
            // Процент
            if (!int.TryParse(fullProgressMatch.Groups[1].Value, out var percent))
                return;

            // Время прошло
            var elapsedStr = fullProgressMatch.Groups[2].Value;
            var elapsedTime = ParseTimeSpanShort(elapsedStr);

            // Время осталось
            var remainingStr = fullProgressMatch.Groups[3].Value;
            var remainingTime = ParseTimeSpanShort(remainingStr);

            // Скорость (audio seconds/s)
            double? speed = null;
            if (double.TryParse(fullProgressMatch.Groups[4].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedSpeed))
            {
                speed = parsedSpeed;
            }

            var message = BuildProgressMessage(percent, elapsedTime, remainingTime, null, _audioDuration, speed);
            RaiseProgress(TranscriptionState.Transcribing, percent, message, elapsedTime, remainingTime, null, _audioDuration, speed);
            return;
        }

        // Fallback: старый формат парсинга
        // Парсим скорость расшифровки (если есть)
        double? fallbackSpeed = null;
        var speedMatch = SpeedRegex().Match(line);
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedFallbackSpeed))
        {
            fallbackSpeed = parsedFallbackSpeed;
        }

        // Парсим процент
        var percentMatch = PercentRegex().Match(line);
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var fallbackPercent))
        {
            var intPercent = (int)Math.Round(fallbackPercent);
            var remaining = intPercent > 0
                ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - intPercent) / intPercent)
                : TimeSpan.Zero;

            var message = BuildProgressMessage(intPercent, elapsed, remaining, null, _audioDuration, fallbackSpeed);

            RaiseProgress(TranscriptionState.Transcribing, intPercent, message, elapsed, remaining, null, _audioDuration, fallbackSpeed);
            return;
        }
    }

    private static TimeSpan ParseTimeSpanShort(string time)
    {
        // Парсит формат mm:ss
        var parts = time.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var minutes) && int.TryParse(parts[1], out var seconds))
        {
            return TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }
        return TimeSpan.Zero;
    }

    private static string BuildProgressMessage(int percent, TimeSpan elapsed, TimeSpan remaining, TimeSpan? processed, TimeSpan? total, double? speed)
    {
        // Основное сообщение - только процент и хронометраж
        // Детали (прошло, скорость, осталось) UI возьмёт из объекта TranscriptionProgress
        if (processed.HasValue && total.HasValue)
        {
            return $"Транскрипция {percent}% • {FormatTimeSpan(processed.Value)} / {FormatTimeSpan(total.Value)}";
        }

        return $"Транскрипция {percent}%";
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"h\:mm\:ss");
        return ts.ToString(@"m\:ss");
    }

    private static async Task<List<TranscriptionSegment>> ParseTranscriptionFileAsync(string txtPath)
    {
        var segments = new List<TranscriptionSegment>();

        // Читаем файл с поддержкой UTF-8 и автоопределением кодировки
        var lines = await File.ReadAllLinesAsync(txtPath, Encoding.UTF8);

        // Формат с диаризацией:
        // [00:00:00.000 --> 00:00:02.500] [SPEAKER_00]: Текст
        // или без диаризации:
        // [00:00:00.000 --> 00:00:02.500] Текст
        // Текст может быть многострочным (продолжение на следующей строке без timestamp)
        var regex = SegmentRegex();

        TimeSpan currentStart = TimeSpan.Zero;
        TimeSpan currentEnd = TimeSpan.Zero;
        string currentSpeaker = "SPEAKER_00";
        var currentText = new StringBuilder();
        bool hasValidSegment = false;

        void AddCurrentSegment()
        {
            var text = currentText.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text) && hasValidSegment)
            {
                segments.Add(new TranscriptionSegment(currentStart, currentEnd, currentSpeaker, text));
            }
            currentText.Clear();
            hasValidSegment = false;
        }

        int lineNum = 0;
        foreach (var rawLine in lines)
        {
            lineNum++;
            // Убираем BOM и невидимые символы
            var line = rawLine.Trim('\uFEFF', '\u200B', '\u200C', '\u200D');

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = regex.Match(line);
            if (match.Success)
            {
                // Сохраняем предыдущий сегмент
                AddCurrentSegment();

                // Начинаем новый сегмент
                var startStr = match.Groups[1].Value;
                var endStr = match.Groups[2].Value;
                currentStart = ParseTimeSpan(startStr);
                currentEnd = ParseTimeSpan(endStr);
                currentSpeaker = match.Groups[3].Success ? match.Groups[3].Value : "SPEAKER_00";
                currentText.Append(match.Groups[4].Value.Trim());
                hasValidSegment = true;

                // Логирование для отладки
                if (currentStart == TimeSpan.Zero && currentEnd == TimeSpan.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[ParseTranscription] Line {lineNum}: Failed to parse timestamps: '{startStr}' --> '{endStr}'");
                }
            }
            else
            {
                // Продолжение предыдущего сегмента (многострочный текст)
                if (hasValidSegment)
                {
                    if (currentText.Length > 0)
                        currentText.Append(' '); // Пробел между строками
                    currentText.Append(line.Trim());
                }
                else
                {
                    // Строка без timestamp и без предыдущего сегмента - пропускаем или логируем
                    System.Diagnostics.Debug.WriteLine($"[ParseTranscription] Line {lineNum}: Skipping orphan line: '{line.Substring(0, Math.Min(50, line.Length))}'");
                }
            }
        }

        // Добавляем последний сегмент
        AddCurrentSegment();

        System.Diagnostics.Debug.WriteLine($"[ParseTranscription] Total segments parsed: {segments.Count} from {lineNum} lines");

        return segments;
    }

    private static TimeSpan ParseTimeSpan(string time)
    {
        if (string.IsNullOrWhiteSpace(time))
        {
            System.Diagnostics.Debug.WriteLine($"[ParseTimeSpan] Empty time string");
            return TimeSpan.Zero;
        }

        // Формат: 00:00:00.000, 00:00:00,000 или 00:00.000 (mm:ss.fff)
        var originalTime = time;
        time = time.Trim().Replace(',', '.');

        // Если формат mm:ss.fff (только 2 двоеточия), добавляем часы
        var parts = time.Split('.');
        if (parts.Length == 2)
        {
            var timePart = parts[0];
            var colonCount = timePart.Count(c => c == ':');
            if (colonCount == 1)
            {
                // Формат mm:ss.fff → 00:mm:ss.fff
                time = "00:" + time;
            }
        }

        // Пытаемся распарсить
        if (TimeSpan.TryParse(time, System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // Если не получилось - логируем для отладки
        System.Diagnostics.Debug.WriteLine($"[ParseTimeSpan] Failed to parse: '{originalTime}' (normalized: '{time}')");
        return TimeSpan.Zero;
    }

    private void RaiseProgress(TranscriptionState state, int percent, string? message,
        TimeSpan? elapsed = null, TimeSpan? remaining = null,
        TimeSpan? processed = null, TimeSpan? total = null, double? speed = null)
    {
        ProgressChanged?.Invoke(this, new TranscriptionProgress(
            state, percent, message, elapsed, remaining, processed, total, speed));
    }

    // Парсит полный формат прогресса faster-whisper-xxl с -pp:
    // "  1% |   35/4423 | 00:01<<02:22 | 30.73 audio seconds/s"
    // Группы: 1=процент, 2=время_прошло, 3=время_осталось, 4=скорость
    [GeneratedRegex(@"^\s*(\d+)%\s*\|.*?\|\s*(\d{2}:\d{2})<<(\d{2}:\d{2})\s*\|\s*([\d.,]+)\s+audio seconds")]
    private static partial Regex FullProgressRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)%")]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2})")]
    private static partial Regex TimeRegex();

    // Парсит скорость: "2.5x" или "speed: 2.5x" или "2.5x realtime"
    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*x")]
    private static partial Regex SpeedRegex();

    // Поддерживает форматы: [00:00:00.000 --> 00:00:02.500] [SPEAKER]: Текст
    // Более гибкий regex с опциональными пробелами
    [GeneratedRegex(@"^\s*\[(\d{2}:\d{2}(?::\d{2})?[.,]\d{3})\s*-->\s*(\d{2}:\d{2}(?::\d{2})?[.,]\d{3})\]\s*(?:\[([^\]]+)\])?\s*:?\s*(.*)$")]
    private static partial Regex SegmentRegex();
}

