using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Infrastructure.AI;

public class VoxtralTranscriptionService : IVoiceTranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly MistralSettings _settings;
    private readonly ILogger<VoxtralTranscriptionService> _logger;

    public VoxtralTranscriptionService(
        HttpClient httpClient,
        IOptions<MistralSettings> settings,
        ILogger<VoxtralTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, string locale)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            var audioContent = new StreamContent(audio);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
            content.Add(audioContent, "file", fileName);
            content.Add(new StringContent("voxtral-mini-latest"), "model");

            // Map locale to language code
            var language = locale switch
            {
                "de" => "de",
                _ => "en"
            };
            content.Add(new StringContent(language), "language");

            _logger.LogDebug("Sending audio transcription request to Voxtral API ({FileName}, locale={Locale})", fileName, locale);

            var response = await _httpClient.PostAsync("/v1/audio/transcriptions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Voxtral API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                throw new Exception($"Voxtral API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<VoxtralResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Text ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error transcribing audio with Voxtral");
            throw;
        }
    }

    private class VoxtralResponse
    {
        public string Text { get; set; } = string.Empty;
    }
}
