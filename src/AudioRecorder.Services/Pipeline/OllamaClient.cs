using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AudioRecorder.Services.Pipeline;

public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly SessionPipelineOptions _options;

    public OllamaClient(SessionPipelineOptions options)
    {
        _options = options;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(30, options.RequestTimeoutSeconds))
        };
    }

    public async Task<string> GenerateSummaryAsync(string systemPrompt, string cleanedText, CancellationToken ct)
    {
        var request = new OllamaGenerateRequest
        {
            Model = _options.Model,
            System = systemPrompt,
            Prompt = cleanedText,
            Stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync(_options.OllamaUrl, request, ct);
        var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: ct);

        if (!response.IsSuccessStatusCode)
        {
            var apiError = payload?.Error;
            throw new HttpRequestException(
                $"Ollama HTTP {(int)response.StatusCode}: {response.ReasonPhrase}. {apiError}".Trim());
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.Response))
        {
            throw new InvalidOperationException("Ollama response payload does not contain `response`.");
        }

        return payload.Response.Trim();
    }
}

public sealed class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("system")]
    public string System { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

public sealed class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
