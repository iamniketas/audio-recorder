using NAudio.Lame;
using NAudio.Wave;

namespace AudioRecorder.Services.Audio;

/// <summary>
/// Конвертер аудиофайлов
/// </summary>
public static class AudioConverter
{
    /// <summary>
    /// Конвертировать WAV в MP3
    /// </summary>
    /// <param name="wavPath">Путь к WAV файлу</param>
    /// <param name="bitrate">Битрейт MP3 (по умолчанию 192 kbps)</param>
    /// <param name="deleteOriginal">Удалить исходный WAV после конвертации</param>
    /// <returns>Путь к MP3 файлу</returns>
    public static async Task<string> ConvertToMp3Async(
        string wavPath,
        int bitrate = 192,
        bool deleteOriginal = true)
    {
        if (!File.Exists(wavPath))
            throw new FileNotFoundException("WAV файл не найден", wavPath);

        var mp3Path = Path.ChangeExtension(wavPath, ".mp3");

        await Task.Run(() =>
        {
            using var reader = new WaveFileReader(wavPath);
            using var writer = new LameMP3FileWriter(mp3Path, reader.WaveFormat, bitrate);
            reader.CopyTo(writer);
        });

        if (deleteOriginal && File.Exists(mp3Path))
        {
            File.Delete(wavPath);
        }

        return mp3Path;
    }

    /// <summary>
    /// Проверить, является ли файл WAV
    /// </summary>
    public static bool IsWavFile(string path)
    {
        return Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверить, является ли файл MP3
    /// </summary>
    public static bool IsMp3File(string path)
    {
        return Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }
}
