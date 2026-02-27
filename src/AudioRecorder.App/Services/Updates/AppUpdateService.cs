using Velopack;
using Velopack.Sources;

namespace AudioRecorder.Services.Updates;

public sealed record UpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string StatusMessage,
    UpdateInfo? UpdateInfo = null);

public sealed record UpdateDownloadResult(
    bool Success,
    string StatusMessage,
    VelopackAsset? ReadyToApplyRelease = null);

public sealed class AppUpdateService : IDisposable
{
    // Repo with GitHub Releases that stores Velopack packages.
    private const string ReleasesRepoUrl = "https://github.com/iamniketas/contora";
    private readonly UpdateManager? _updateManager;

    public AppUpdateService()
    {
        try
        {
            var source = new GithubSource(ReleasesRepoUrl, null, false);
            _updateManager = new UpdateManager(source);
        }
        catch
        {
            _updateManager = null;
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        if (_updateManager == null)
            return new UpdateCheckResult(false, false, "Update service is unavailable.");

        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
                return new UpdateCheckResult(true, false, "You already have the latest version.");

            var version = updateInfo.TargetFullRelease.Version;
            return new UpdateCheckResult(
                true,
                true,
                $"Version {version} is available.",
                updateInfo);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, false, $"Failed to check for updates: {ex.Message}");
        }
    }

    public async Task<UpdateDownloadResult> DownloadUpdateAsync(
        UpdateInfo updateInfo,
        Action<int> onProgress)
    {
        if (_updateManager == null)
            return new UpdateDownloadResult(false, "Update service is unavailable.");

        try
        {
            await _updateManager.DownloadUpdatesAsync(updateInfo, onProgress);
            return new UpdateDownloadResult(
                true,
                "Update downloaded. Click \"Apply update\".",
                updateInfo.TargetFullRelease);
        }
        catch (Exception ex)
        {
            return new UpdateDownloadResult(false, $"Failed to download update: {ex.Message}");
        }
    }

    public bool ApplyUpdateAndRestart(VelopackAsset releaseToApply)
    {
        if (_updateManager == null)
            return false;

        try
        {
            _updateManager.WaitExitThenApplyUpdates(releaseToApply);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // UpdateManager in current Velopack version is not IDisposable.
    }
}

