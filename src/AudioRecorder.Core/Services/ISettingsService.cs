namespace AudioRecorder.Core.Services;

public interface ISettingsService
{
    void SaveSelectedSourceIds(IEnumerable<string> sourceIds);

    IReadOnlyList<string> LoadSelectedSourceIds();

    void SaveOutputFolder(string folderPath);

    string? LoadOutputFolder();

    void SaveTranscriptionMode(string mode);

    string LoadTranscriptionMode();

    void SaveWhisperModel(string modelName);

    string LoadWhisperModel();
}
