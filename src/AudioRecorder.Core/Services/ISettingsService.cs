namespace AudioRecorder.Core.Services;

/// <summary>
/// Сервис для сохранения и загрузки настроек приложения
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Сохранить список ID выбранных источников
    /// </summary>
    void SaveSelectedSourceIds(IEnumerable<string> sourceIds);

    /// <summary>
    /// Загрузить список ID выбранных источников
    /// </summary>
    IReadOnlyList<string> LoadSelectedSourceIds();
}
