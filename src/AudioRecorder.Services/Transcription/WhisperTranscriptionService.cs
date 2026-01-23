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
    private TimeSpan _audioDuration;
    private DateTime _transcriptionStartTime;

    public event EventHandler<TranscriptionProgress>? ProgressChanged;

    public bool IsWhisperAvailable => File.Exists(_whisperPath);

    public WhisperTranscriptionService(string? whisperPath = null, string modelName = "large-v2")
    {
        _whisperPath = whisperPath ?? GetDefaultWhisperPath();
        _modelName = modelName;
    }

    private static string GetDefaultWhisperPath()
    {
        // Ищем в tools/ относительно exe
        var exeDir = AppContext.BaseDirectory;
        var toolsPath = Path.Combine(exeDir, "tools", "faster-whisper-xxl", "faster-whisper-xxl.exe");

        if (File.Exists(toolsPath))
            return toolsPath;

        // Ищем в корне проекта (для разработки)
        var projectRoot = FindProjectRoot(exeDir);
        if (projectRoot != null)
        {
            toolsPath = Path.Combine(projectRoot, "tools", "faster-whisper-xxl", "faster-whisper-xxl.exe");
            if (File.Exists(toolsPath))
                return toolsPath;
        }

        return toolsPath; // Вернём путь даже если не существует
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
                tempOutputDir = Path.Combine(Path.GetTempPath(), $"AudioRecorder_{Guid.NewGuid():N}");
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
            var result = await RunWhisperAsync(processPath, outputDir, originalFileName, ct);

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

    private async Task<TranscriptionResult> RunWhisperAsync(string audioPath, string outputDir, string outputFileName, CancellationToken ct)
    {
        var arguments = BuildArguments(audioPath, outputDir);

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
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            outputBuilder.AppendLine(e.Data);
            ParseProgressFromOutput(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                // faster-whisper-xxl выводит прогресс в stderr
                ParseProgressFromOutput(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            if (string.IsNullOrWhiteSpace(error))
                error = $"Whisper завершился с кодом {process.ExitCode}";
            return new TranscriptionResult(false, null, [], error);
        }

        // Ищем выходной файл (Whisper создаёт файл с именем аудио)
        var whisperBaseName = Path.GetFileNameWithoutExtension(audioPath);
        var whisperTxtPath = Path.Combine(outputDir, $"{whisperBaseName}.txt");

        if (!File.Exists(whisperTxtPath))
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
        // faster-whisper-xxl -o {outputDir} -f txt -m large-v2 --diarize pyannote_v3.1 --language ru {audioPath}
        // Для длинных файлов добавляем:
        // --vad_filter true — пропуск тишины, снижает нагрузку на память
        // --chunk_length 30 — обработка частями по 30 секунд (предотвращает OOM на длинных файлах)
        // --compute_type float16 — экономия VRAM (или int8 для ещё большей экономии)
        var sb = new StringBuilder();
        sb.Append($"-o \"{outputDir}\" ");
        sb.Append("-f txt ");
        sb.Append($"-m {_modelName} ");
        sb.Append("--vad_filter true ");
        sb.Append("--chunk_length 30 ");
        sb.Append("--compute_type float16 ");
        sb.Append("--diarize pyannote_v3.1 ");
        sb.Append("--language ru ");
        sb.Append($"\"{audioPath}\"");
        return sb.ToString();
    }

    private void ParseProgressFromOutput(string line)
    {
        // faster-whisper-xxl выводит прогресс в разных форматах:
        // "Transcribing: 45.2%" или просто "45%"
        // Также выводит временные метки: "[00:01:23.456 --> 00:01:25.789]"

        // Парсим процент
        var percentMatch = PercentRegex().Match(line);
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var percent))
        {
            var intPercent = (int)Math.Round(percent);
            var elapsed = DateTime.Now - _transcriptionStartTime;
            var remaining = intPercent > 0
                ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - intPercent) / intPercent)
                : TimeSpan.Zero;

            var message = remaining.TotalSeconds > 5
                ? $"Транскрипция... {intPercent}% (осталось ~{FormatTimeSpan(remaining)})"
                : $"Транскрипция... {intPercent}%";

            RaiseProgress(TranscriptionState.Transcribing, intPercent, message);
            return;
        }

        // Парсим временные метки для оценки прогресса
        var timeMatch = TimeRegex().Match(line);
        if (timeMatch.Success)
        {
            var currentTime = ParseTimeSpan(timeMatch.Groups[1].Value);
            if (_audioDuration.TotalSeconds > 0)
            {
                var progressPercent = (int)(currentTime.TotalSeconds / _audioDuration.TotalSeconds * 100);
                progressPercent = Math.Min(progressPercent, 99);

                var elapsed = DateTime.Now - _transcriptionStartTime;
                var remaining = progressPercent > 0
                    ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - progressPercent) / progressPercent)
                    : TimeSpan.Zero;

                var message = remaining.TotalSeconds > 5
                    ? $"Обработано {FormatTimeSpan(currentTime)} / {FormatTimeSpan(_audioDuration)} (~{FormatTimeSpan(remaining)} осталось)"
                    : $"Обработано {FormatTimeSpan(currentTime)} / {FormatTimeSpan(_audioDuration)}";

                RaiseProgress(TranscriptionState.Transcribing, progressPercent, message);
            }
            else
            {
                RaiseProgress(TranscriptionState.Transcribing, -1, $"Обработано {FormatTimeSpan(currentTime)}");
            }
        }
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
        var lines = await File.ReadAllLinesAsync(txtPath);

        // Формат с диаризацией:
        // [00:00:00.000 --> 00:00:02.500] [SPEAKER_00] Текст
        // или без диаризации:
        // [00:00:00.000 --> 00:00:02.500] Текст
        var regex = SegmentRegex();

        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                var start = ParseTimeSpan(match.Groups[1].Value);
                var end = ParseTimeSpan(match.Groups[2].Value);
                var speaker = match.Groups[3].Success ? match.Groups[3].Value : "SPEAKER_00";
                var text = match.Groups[4].Value.Trim();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new TranscriptionSegment(start, end, speaker, text));
                }
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                // Простой текст без временных меток
                segments.Add(new TranscriptionSegment(TimeSpan.Zero, TimeSpan.Zero, "SPEAKER_00", line.Trim()));
            }
        }

        return segments;
    }

    private static TimeSpan ParseTimeSpan(string time)
    {
        // Формат: 00:00:00.000 или 00:00:00,000
        time = time.Replace(',', '.');
        if (TimeSpan.TryParse(time, out var result))
            return result;
        return TimeSpan.Zero;
    }

    private void RaiseProgress(TranscriptionState state, int percent, string? message)
    {
        ProgressChanged?.Invoke(this, new TranscriptionProgress(state, percent, message));
    }

    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex PercentRegex();

    [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2})")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2}[.,]\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}[.,]\d{3})\]\s*(?:\[([^\]]+)\])?\s*(.*)")]
    private static partial Regex SegmentRegex();
}
