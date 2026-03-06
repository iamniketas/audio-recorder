using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AudioRecorder.Services.Transcription;

namespace AudioRecorder.Services.Models;

public sealed record RuntimeInstallProgress(
    string Stage,
    int Percent,
    string StatusMessage,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    double SpeedBytesPerSecond = 0,
    TimeSpan? Eta = null);

public sealed record RuntimeInstallResult(
    bool Success,
    string StatusMessage,
    string? WhisperExePath = null);

public sealed class WhisperRuntimeInstallerService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/Purfview/whisper-standalone-win/releases?per_page=20";
    private const string TargetTag = "Faster-Whisper-XXL";
    private const string SevenZipDownloadUrl = "https://www.7-zip.org/a/7zr.exe";

    private readonly string _runtimeRoot;
    private readonly string _runtimeExePath;

    /// <param name="customInstallRoot">
    /// Custom directory under which the "faster-whisper-xxl" folder will be created.
    /// Pass null to use the default canonical location (%LocalAppData%\Contora\runtime\).
    /// </param>
    public WhisperRuntimeInstallerService(string? customInstallRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(customInstallRoot))
        {
            _runtimeRoot = Path.Combine(customInstallRoot, "faster-whisper-xxl");
            _runtimeExePath = Path.Combine(_runtimeRoot, "faster-whisper-xxl.exe");
        }
        else
        {
            _runtimeRoot = WhisperPaths.GetCanonicalRuntimeRoot();
            _runtimeExePath = WhisperPaths.GetCanonicalWhisperPath();
        }
    }

    public bool IsRuntimeInstalled()
    {
        // Check the configured path first, then fall back to env-var resolution.
        if (File.Exists(_runtimeExePath)) return true;
        var defaultPath = WhisperPaths.GetDefaultWhisperPath();
        return File.Exists(defaultPath);
    }

    public string GetRuntimeExePath()
    {
        if (File.Exists(_runtimeExePath)) return _runtimeExePath;
        return WhisperPaths.GetDefaultWhisperPath();
    }

    /// <summary>Returns the directory where the runtime will be (or is) installed.</summary>
    public string GetRuntimeRoot() => _runtimeRoot;

    /// <summary>Minimum runtime version required for diarization support.</summary>
    public static readonly Version MinimumRequiredVersion = new(245, 0);

    /// <summary>
    /// Runs faster-whisper-xxl.exe --version and parses the release number.
    /// Returns null if the runtime is not installed or version cannot be determined.
    /// Output format: "Standalone Faster-Whisper-XXL r245.4"
    /// </summary>
    public async Task<(Version? Version, string? VersionString)> GetInstalledVersionAsync()
    {
        if (!IsRuntimeInstalled())
            return (null, null);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _runtimeExePath,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            // Wait max 10 seconds
            var exited = process.WaitForExit(10_000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                return (null, null);
            }

            var combined = $"{output} {stderr}";
            // Match "rNNN.N.N" or "rNNN.N"
            var match = Regex.Match(combined, @"r(\d+(?:\.\d+)*)");
            if (match.Success)
            {
                var versionStr = match.Groups[1].Value;
                var version = ParseLooseVersion(versionStr);
                return (version, $"r{versionStr}");
            }

            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<RuntimeInstallResult> InstallAsync(
        Action<RuntimeInstallProgress> onProgress,
        CancellationToken ct)
    {
        try
        {
            onProgress(new RuntimeInstallProgress("resolve", 1, "Resolving latest faster-whisper-xxl release..."));
            var assetUrl = await ResolveWindowsAssetUrlAsync(ct);
            if (assetUrl == null)
            {
                return new RuntimeInstallResult(false, "Could not find a Windows faster-whisper-xxl asset in official releases.");
            }

            var sevenZipExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Contora", "tools", "7zr.exe");

            if (!File.Exists(sevenZipExe))
            {
                onProgress(new RuntimeInstallProgress("download-7zr", 3, "Скачивание 7zr.exe (~1.3 МБ)..."));
                Directory.CreateDirectory(Path.GetDirectoryName(sevenZipExe)!);
                await DownloadFileAsync(SevenZipDownloadUrl, sevenZipExe,
                    p => onProgress(p with { Stage = "download-7zr" }), ct,
                    stage: "download-7zr", percentBase: 3, percentRange: 1);
            }

            onProgress(new RuntimeInstallProgress("download-7zr", 4, "7zr.exe готов."));

            var tempArchive = Path.Combine(Path.GetTempPath(), $"faster-whisper-xxl_{Guid.NewGuid():N}.7z");
            var tempExtractDir = Path.Combine(Path.GetTempPath(), $"faster-whisper-xxl_extract_{Guid.NewGuid():N}");

            try
            {
                onProgress(new RuntimeInstallProgress("download", 5, "Скачивание Whisper XXL runtime..."));
                await DownloadFileAsync(assetUrl, tempArchive,
                    p => onProgress(p with { Stage = "download" }), ct,
                    stage: "download", percentBase: 5, percentRange: 65);

                onProgress(new RuntimeInstallProgress("extract", 72, "Extracting runtime archive..."));
                Directory.CreateDirectory(tempExtractDir);
                await ExtractArchiveAsync(sevenZipExe, tempArchive, tempExtractDir, p =>
                {
                    var mapped = 72 + (int)(p * 0.23); // 72..95
                    onProgress(new RuntimeInstallProgress("extract", mapped, $"Extracting runtime... {p}%"));
                }, ct);

                var extractedExe = FindWhisperExe(tempExtractDir);
                if (extractedExe == null)
                {
                    return new RuntimeInstallResult(false, "Archive was downloaded, but faster-whisper-xxl.exe was not found.");
                }

                var extractedRoot = Path.GetDirectoryName(extractedExe)!;
                InstallExtractedRuntime(extractedRoot);

                WhisperPaths.RegisterEnvironmentVariables(_runtimeExePath, "large-v2");
                onProgress(new RuntimeInstallProgress("done", 100, "Whisper XXL runtime installed."));
                return new RuntimeInstallResult(true, "Whisper XXL runtime installed.", _runtimeExePath);
            }
            finally
            {
                TryDelete(tempArchive);
                TryDeleteDirectory(tempExtractDir);
            }
        }
        catch (OperationCanceledException)
        {
            return new RuntimeInstallResult(false, "Runtime installation cancelled.");
        }
        catch (Exception ex)
        {
            return new RuntimeInstallResult(false, $"Failed to install runtime: {ex.Message}");
        }
    }

    private async Task<string?> ResolveWindowsAssetUrlAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Add("User-Agent", "Contora");

        var json = await http.GetStringAsync(ReleasesApiUrl, ct);
        using var doc = JsonDocument.Parse(json);

        string? bestUrl = null;
        Version bestVersion = new(0, 0);

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (!release.TryGetProperty("tag_name", out var tagElement))
                continue;

            var tag = tagElement.GetString();
            if (!string.Equals(tag, TargetTag, StringComparison.OrdinalIgnoreCase))
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

                // Extract version from filename like "Faster-Whisper-XXL_r245.4_windows.7z"
                var versionStr = name
                    .Replace("Faster-Whisper-XXL_r", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("_windows.7z", "", StringComparison.OrdinalIgnoreCase);
                var assetVersion = ParseLooseVersion(versionStr);

                if (assetVersion > bestVersion)
                {
                    bestVersion = assetVersion;
                    bestUrl = url;
                }
            }
        }

        return bestUrl;
    }

    private static Version ParseLooseVersion(string versionStr)
    {
        // Handle versions like "245.4", "192.3.4" by padding to at least Major.Minor
        var parts = versionStr.Split('.');
        int major = 0, minor = 0, build = 0;
        if (parts.Length >= 1) int.TryParse(parts[0], out major);
        if (parts.Length >= 2) int.TryParse(parts[1], out minor);
        if (parts.Length >= 3) int.TryParse(parts[2], out build);
        return new Version(major, minor, build);
    }

    private static async Task DownloadFileAsync(
        string url,
        string outputPath,
        Action<RuntimeInstallProgress> onProgress,
        CancellationToken ct,
        string stage = "download",
        int percentBase = 5,
        int percentRange = 65)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.Add("User-Agent", "Contora");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        long downloadedBytes = 0;
        var downloadStart = DateTime.UtcNow;

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

            var elapsed = (DateTime.UtcNow - downloadStart).TotalSeconds;
            var speed = elapsed > 0.5 ? downloadedBytes / elapsed : 0;
            var eta = speed > 0 && totalBytes > downloadedBytes
                ? TimeSpan.FromSeconds((totalBytes - downloadedBytes) / speed)
                : (TimeSpan?)null;

            int mappedPercent = percentBase;
            string statusMsg = "Downloading...";
            if (totalBytes > 0)
            {
                var rawPercent = (double)downloadedBytes / totalBytes;
                mappedPercent = percentBase + (int)(rawPercent * percentRange);

                var dlMb = downloadedBytes / (1024.0 * 1024.0);
                var totMb = totalBytes / (1024.0 * 1024.0);
                var speedMb = speed / (1024.0 * 1024.0);
                statusMsg = eta.HasValue
                    ? $"{dlMb:F1}/{totMb:F1} МБ — {speedMb:F1} МБ/с — осталось {FormatEta(eta.Value)}"
                    : $"{dlMb:F1}/{totMb:F1} МБ — {speedMb:F1} МБ/с";
            }

            onProgress(new RuntimeInstallProgress(stage, Math.Min(percentBase + percentRange, mappedPercent),
                statusMsg, downloadedBytes, totalBytes, speed, eta));
        }
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1) return $"{(int)eta.TotalHours}ч {eta.Minutes}м";
        if (eta.TotalMinutes >= 1) return $"{(int)eta.TotalMinutes}м {eta.Seconds}с";
        return $"{eta.Seconds}с";
    }

    private static async Task ExtractArchiveAsync(
        string sevenZipExe,
        string archivePath,
        string destinationDir,
        Action<int> onPercent,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = sevenZipExe,
            Arguments = $"x \"{archivePath}\" -o\"{destinationDir}\" -y -bsp1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>();

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        using var registration = ct.Register(() =>
        {
            try { process.Kill(); } catch { }
            tcs.TrySetCanceled(ct);
        });

        process.Start();

        // Parse stdout for percentage lines like " 42%" or "100%"
        var percentRegex = new Regex(@"(\d+)%", RegexOptions.Compiled);
        var lastPercent = 0;

        _ = Task.Run(async () =>
        {
            try
            {
                var buffer = new char[256];
                var lineBuffer = new System.Text.StringBuilder();

                while (!process.StandardOutput.EndOfStream)
                {
                    var read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    lineBuffer.Append(buffer, 0, read);
                    var text = lineBuffer.ToString();

                    // 7zr uses \r for progress updates (no newline)
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var match = percentRegex.Match(line);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var pct))
                        {
                            if (pct > lastPercent)
                            {
                                lastPercent = pct;
                                onPercent(pct);
                            }
                        }
                    }

                    // Keep only the last incomplete segment
                    var lastSep = text.LastIndexOfAny(new[] { '\r', '\n' });
                    if (lastSep >= 0)
                        lineBuffer.Clear().Append(text[(lastSep + 1)..]);
                }
            }
            catch { }
        }, ct);

        // Drain stderr
        _ = process.StandardError.ReadToEndAsync();

        var exitCode = await tcs.Task;

        if (exitCode != 0)
            throw new InvalidOperationException($"7zr.exe exited with code {exitCode}.");
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
