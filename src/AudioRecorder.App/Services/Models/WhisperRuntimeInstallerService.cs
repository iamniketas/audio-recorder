using System.Text.Json;
using AudioRecorder.Services.Transcription;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace AudioRecorder.Services.Models;

public sealed record RuntimeInstallProgress(
    string Stage,
    int Percent,
    string StatusMessage);

public sealed record RuntimeInstallResult(
    bool Success,
    string StatusMessage,
    string? WhisperExePath = null);

public sealed class WhisperRuntimeInstallerService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/Purfview/whisper-standalone-win/releases?per_page=20";
    private const string TargetTag = "Faster-Whisper-XXL";

    private readonly string _runtimeRoot;
    private readonly string _runtimeExePath;

    public WhisperRuntimeInstallerService()
    {
        _runtimeRoot = WhisperPaths.GetCanonicalRuntimeRoot();
        _runtimeExePath = WhisperPaths.GetCanonicalWhisperPath();
    }

    public bool IsRuntimeInstalled()
    {
        return File.Exists(_runtimeExePath);
    }

    public string GetRuntimeExePath() => _runtimeExePath;

    public async Task<RuntimeInstallResult> InstallAsync(
        Action<RuntimeInstallProgress> onProgress,
        CancellationToken ct)
    {
        try
        {
            onProgress(new RuntimeInstallProgress("resolve", 1, "Получаем актуальный релиз faster-whisper-xxl..."));
            var assetUrl = await ResolveWindowsAssetUrlAsync(ct);
            if (assetUrl == null)
            {
                return new RuntimeInstallResult(false, "Не удалось найти Windows-ассет faster-whisper-xxl в официальных релизах.");
            }

            var tempArchive = Path.Combine(Path.GetTempPath(), $"faster-whisper-xxl_{Guid.NewGuid():N}.7z");
            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"faster-whisper-xxl_extract_{Guid.NewGuid():N}");

            try
            {
                onProgress(new RuntimeInstallProgress("download", 5, "Скачиваем движок Whisper XXL..."));
                await DownloadFileAsync(assetUrl, tempArchive, progress =>
                {
                    var mapped = 5 + (int)(progress * 0.65); // 5..70
                    onProgress(new RuntimeInstallProgress("download", mapped, $"Скачиваем движок Whisper XXL... {progress}%"));
                }, ct);

                onProgress(new RuntimeInstallProgress("extract", 72, "Распаковываем архив движка..."));
                Directory.CreateDirectory(tempExtractDir);
                ExtractArchive(tempArchive, tempExtractDir, p =>
                {
                    var mapped = 72 + (int)(p * 0.23); // 72..95
                    onProgress(new RuntimeInstallProgress("extract", mapped, $"Распаковка движка... {p}%"));
                });

                var extractedExe = FindWhisperExe(tempExtractDir);
                if (extractedExe == null)
                {
                    return new RuntimeInstallResult(false, "Архив скачан, но файл faster-whisper-xxl.exe не найден.");
                }

                var extractedRoot = Path.GetDirectoryName(extractedExe)!;
                InstallExtractedRuntime(extractedRoot);

                WhisperPaths.RegisterEnvironmentVariables(_runtimeExePath, "large-v2");
                onProgress(new RuntimeInstallProgress("done", 100, "Движок Whisper XXL установлен."));
                return new RuntimeInstallResult(true, "Движок Whisper XXL установлен.", _runtimeExePath);
            }
            finally
            {
                TryDelete(tempArchive);
                TryDeleteDirectory(tempExtractDir);
            }
        }
        catch (OperationCanceledException)
        {
            return new RuntimeInstallResult(false, "Установка движка отменена.");
        }
        catch (Exception ex)
        {
            return new RuntimeInstallResult(false, $"Не удалось установить движок: {ex.Message}");
        }
    }

    private async Task<string?> ResolveWindowsAssetUrlAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder");

        var json = await http.GetStringAsync(ReleasesApiUrl, ct);
        using var doc = JsonDocument.Parse(json);

        string? bestUrl = null;
        DateTimeOffset bestPublishedAt = DateTimeOffset.MinValue;

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (!release.TryGetProperty("tag_name", out var tagElement))
                continue;

            var tag = tagElement.GetString();
            if (!string.Equals(tag, TargetTag, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!release.TryGetProperty("published_at", out var publishedAtEl))
                continue;

            if (!DateTimeOffset.TryParse(publishedAtEl.GetString(), out var publishedAt))
                continue;

            if (!release.TryGetProperty("assets", out var assetsEl))
                continue;

            foreach (var asset in assetsEl.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!name.EndsWith("_windows.7z", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!name.StartsWith("Faster-Whisper-XXL_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = asset.GetProperty("browser_download_url").GetString();
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                if (publishedAt > bestPublishedAt)
                {
                    bestPublishedAt = publishedAt;
                    bestUrl = url;
                }
            }
        }

        return bestUrl;
    }

    private static async Task DownloadFileAsync(
        string url,
        string outputPath,
        Action<int> onPercent,
        CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.Add("User-Agent", "AudioRecorder");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        long downloadedBytes = 0;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var destination = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[1024 * 64];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
                break;

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            downloadedBytes += read;
            if (totalBytes > 0)
            {
                var percent = (int)(downloadedBytes * 100 / totalBytes);
                onPercent(Math.Max(0, Math.Min(100, percent)));
            }
        }
    }

    private static void ExtractArchive(string archivePath, string destinationDir, Action<int> onPercent)
    {
        using var archive = SevenZipArchive.Open(archivePath);
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        var total = Math.Max(entries.Count, 1);
        var index = 0;

        foreach (var entry in entries)
        {
            var relativePath = (entry.Key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(destinationDir, relativePath));
            var destinationRoot = Path.GetFullPath(destinationDir);

            if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Archive path traversal detected.");

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var entryStream = entry.OpenEntryStream();
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(fileStream);

            index++;
            var percent = (int)(index * 100.0 / total);
            onPercent(percent);
        }
    }

    private void InstallExtractedRuntime(string extractedRoot)
    {
        if (Directory.Exists(_runtimeRoot))
            Directory.Delete(_runtimeRoot, recursive: true);

        Directory.CreateDirectory(Path.GetDirectoryName(_runtimeRoot)!);
        Directory.Move(extractedRoot, _runtimeRoot);
    }

    private static string? FindWhisperExe(string rootDir)
    {
        return Directory.EnumerateFiles(rootDir, "faster-whisper-xxl.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
