using System.IO.Compression;
using System.Text.Json;

namespace AudioRecorder.Services.Models;

public sealed record FfmpegInstallProgress(
    string Stage,
    int Percent,
    string StatusMessage);

public sealed record FfmpegInstallResult(
    bool Success,
    string StatusMessage,
    string? FfmpegExePath = null);

public sealed class FfmpegInstallerService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases?per_page=10";
    private const string AssetName = "ffmpeg-master-latest-win64-gpl.zip";

    private readonly string _runtimeRoot;
    private readonly string _ffmpegExePath;

    public FfmpegInstallerService()
    {
        _runtimeRoot = GetCanonicalRuntimeRoot();
        _ffmpegExePath = GetCanonicalFfmpegPath();
    }

    public static string GetCanonicalRuntimeRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Contora", "runtime", "ffmpeg");
    }

    public static string GetCanonicalFfmpegPath()
    {
        return Path.Combine(GetCanonicalRuntimeRoot(), "ffmpeg.exe");
    }

    public bool IsInstalled()
    {
        return File.Exists(_ffmpegExePath);
    }

    public string GetInstalledPath() => _ffmpegExePath;

    public async Task<FfmpegInstallResult> InstallAsync(
        Action<FfmpegInstallProgress> onProgress,
        CancellationToken ct)
    {
        try
        {
            onProgress(new FfmpegInstallProgress("resolve", 1, "Resolving latest FFmpeg release..."));
            var assetUrl = await ResolveAssetUrlAsync(ct);
            if (assetUrl == null)
            {
                return new FfmpegInstallResult(false, "Could not find FFmpeg Windows asset in official releases.");
            }

            var tempArchive = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");
            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"ffmpeg_extract_{Guid.NewGuid():N}");

            try
            {
                onProgress(new FfmpegInstallProgress("download", 5, "Downloading FFmpeg..."));
                await DownloadFileAsync(assetUrl, tempArchive, progress =>
                {
                    var mapped = 5 + (int)(progress * 0.65); // 5..70
                    onProgress(new FfmpegInstallProgress("download", mapped, $"Downloading FFmpeg... {progress}%"));
                }, ct);

                onProgress(new FfmpegInstallProgress("extract", 72, "Extracting FFmpeg..."));
                Directory.CreateDirectory(tempExtractDir);
                ZipFile.ExtractToDirectory(tempArchive, tempExtractDir);

                var extractedExe = FindFfmpegExe(tempExtractDir);
                if (extractedExe == null)
                {
                    return new FfmpegInstallResult(false, "Archive was downloaded, but ffmpeg.exe was not found inside.");
                }

                onProgress(new FfmpegInstallProgress("install", 95, "Installing FFmpeg..."));
                InstallExtractedFfmpeg(extractedExe);

                onProgress(new FfmpegInstallProgress("done", 100, "FFmpeg installed."));
                return new FfmpegInstallResult(true, "FFmpeg installed.", _ffmpegExePath);
            }
            finally
            {
                TryDelete(tempArchive);
                TryDeleteDirectory(tempExtractDir);
            }
        }
        catch (OperationCanceledException)
        {
            return new FfmpegInstallResult(false, "FFmpeg installation cancelled.");
        }
        catch (Exception ex)
        {
            return new FfmpegInstallResult(false, $"Failed to install FFmpeg: {ex.Message}");
        }
    }

    private static async Task<string?> ResolveAssetUrlAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Add("User-Agent", "Contora");

        var json = await http.GetStringAsync(ReleasesApiUrl, ct);
        using var doc = JsonDocument.Parse(json);

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (!release.TryGetProperty("assets", out var assetsEl))
                continue;

            foreach (var asset in assetsEl.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = asset.GetProperty("browser_download_url").GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }

        return null;
    }

    private static async Task DownloadFileAsync(
        string url,
        string outputPath,
        Action<int> onPercent,
        CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.Add("User-Agent", "Contora");
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

    private void InstallExtractedFfmpeg(string extractedExePath)
    {
        if (Directory.Exists(_runtimeRoot))
            Directory.Delete(_runtimeRoot, recursive: true);

        Directory.CreateDirectory(_runtimeRoot);
        File.Copy(extractedExePath, _ffmpegExePath, overwrite: true);
    }

    private static string? FindFfmpegExe(string rootDir)
    {
        return Directory.EnumerateFiles(rootDir, "ffmpeg.exe", SearchOption.AllDirectories)
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
