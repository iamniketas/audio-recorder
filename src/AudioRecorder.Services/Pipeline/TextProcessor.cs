using System.Text.RegularExpressions;

namespace AudioRecorder.Services.Pipeline;

public static partial class TextProcessor
{
    public static string Clean(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var withoutTimestamps = TimestampRegex().Replace(rawText, string.Empty);
        var withSpeakerNames = withoutTimestamps
            .Replace("SPEAKER_00", "Я:")
            .Replace("SPEAKER_01", "Психолог:");

        var lines = withSpeakerNames
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, lines);
    }

    [GeneratedRegex(@"\[\d{2}:\d{2}:\d{2}(?:[.,]\d{1,3})?\]")]
    private static partial Regex TimestampRegex();
}
