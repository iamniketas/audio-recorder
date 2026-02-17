using AudioRecorder.Services.Transcription;

namespace AudioRecorder.Services.Models;

public sealed record ModelDownloadProgress(
    int Percent,
    long DownloadedBytes,
    long TotalBytes,
    string CurrentFile);

public sealed record ModelDownloadResult(
    bool Success,
    string StatusMessage);

public sealed class WhisperModelDownloadService
{
    private const string ModelName = "large-v2";
    private const string ModelRepoResolveUrl = "https://huggingface.co/Systran/faster-whisper-large-v2/resolve/main";

    private static readonly string[] ModelFiles =
    [
        "config.json",
        "tokenizer.json",
        "vocabulary.txt",
        "model.bin"
    ];

    private readonly string _whisperPath;
    private readonly string _modelDir;

    public WhisperModelDownloadService(string? whisperPath = null)
    {
        _whisperPath = whisperPath ?? WhisperPaths.GetDefaultWhisperPath();
        _modelDir = WhisperPaths.GetModelDirectory(_whisperPath, ModelName);
    }

    public bool IsWhisperAvailable => File.Exists(_whisperPath);

    public bool IsModelInstalled()
    {
        return WhisperPaths.IsModelInstalled(_whisperPath, ModelName);
    }

    public string GetWhisperPath() => _whisperPath;

    public string GetModelDirectory() => _modelDir;

    public async Task<ModelDownloadResult> DownloadModelAsync(
        Action<ModelDownloadProgress> onProgress,
        CancellationToken ct)
    {
        if (!IsWhisperAvailable)
        {
            return new ModelDownloadResult(
                false,
                $"Whisper не найден: {_whisperPath}");
        }

        try
        {
            Directory.CreateDirectory(_modelDir);

            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            var totalBytes = await GetTotalSizeAsync(http, ct);
            long downloadedBytes = 0;

            foreach (var fileName in ModelFiles)
            {
                ct.ThrowIfCancellationRequested();

                var tempPath = Path.Combine(_modelDir, $"{fileName}.download");
                var targetPath = Path.Combine(_modelDir, fileName);
                var fileUrl = $"{ModelRepoResolveUrl}/{fileName}";

                if (File.Exists(targetPath))
                {
                    downloadedBytes += new FileInfo(targetPath).Length;
                    var skippedPercent = totalBytes > 0
                        ? (int)(downloadedBytes * 100 / totalBytes)
                        : 0;
                    onProgress(new ModelDownloadProgress(skippedPercent, downloadedBytes, totalBytes, fileName));
                    continue;
                }

                using var response = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[1024 * 64];
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0)
                        break;

                    await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloadedBytes += read;

                    var percent = totalBytes > 0
                        ? (int)(downloadedBytes * 100 / totalBytes)
                        : 0;
                    onProgress(new ModelDownloadProgress(percent, downloadedBytes, totalBytes, fileName));
                }

                await destination.FlushAsync(ct);

                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                File.Move(tempPath, targetPath);
            }

            return new ModelDownloadResult(true, "Модель загружена и готова к использованию.");
        }
        catch (OperationCanceledException)
        {
            return new ModelDownloadResult(false, "Загрузка модели отменена.");
        }
        catch (Exception ex)
        {
            return new ModelDownloadResult(false, $"Не удалось скачать модель: {ex.Message}");
        }
    }

    private static async Task<long> GetTotalSizeAsync(HttpClient http, CancellationToken ct)
    {
        long total = 0;
        foreach (var fileName in ModelFiles)
        {
            var fileUrl = $"{ModelRepoResolveUrl}/{fileName}";
            using var request = new HttpRequestMessage(HttpMethod.Head, fileUrl);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
            {
                total += response.Content.Headers.ContentLength.Value;
            }
        }

        return total;
    }
}
