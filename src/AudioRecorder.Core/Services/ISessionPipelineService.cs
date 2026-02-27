using AudioRecorder.Core.Models;

namespace AudioRecorder.Core.Services;

/// <summary>
/// Post-processing pipeline for transcript text.
/// </summary>
public interface ISessionPipelineService
{
    Task<SessionPipelineResult> ProcessSessionAsync(
        string rawWhisperText,
        string? transcriptionPath,
        CancellationToken ct = default);
}
