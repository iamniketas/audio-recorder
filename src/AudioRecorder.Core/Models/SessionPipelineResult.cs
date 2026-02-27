namespace AudioRecorder.Core.Models;

/// <summary>
/// Result of post-processing pipeline (clean -> LLM -> markdown append).
/// </summary>
public record SessionPipelineResult(
    bool Success,
    string CleanedText,
    string? SummaryText,
    string TargetPath,
    bool UsedBackup,
    string? ErrorMessage
);
