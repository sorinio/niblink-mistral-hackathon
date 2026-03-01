using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Infrastructure.AI;

public class MistralChatClient : IMistralChatClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MistralSettings _settings;
    private readonly ILogger<MistralChatClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MistralChatClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MistralSettings> settings,
        ILogger<MistralChatClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> ChatAsync(List<object> messages, int maxTokens = 2048, bool useLocal = false)
    {
        var (baseUrl, model) = GetEndpoint(useLocal);

        var request = new
        {
            model,
            max_tokens = maxTokens,
            messages
        };

        var client = CreateClient(baseUrl, useLocal);
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending chat request to {BaseUrl} (model: {Model}, local: {UseLocal})", baseUrl, model, useLocal);

        var response = await client.PostAsync("/v1/chat/completions", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Chat API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Chat API error {response.StatusCode}: {responseBody}");
        }

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, JsonOptions);
        return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        List<object> messages,
        int maxTokens = 2048,
        bool useLocal = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (baseUrl, model) = GetEndpoint(useLocal);

        var request = new
        {
            model,
            max_tokens = maxTokens,
            messages,
            stream = true
        };

        var client = CreateClient(baseUrl, useLocal);
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        httpRequest.Content = httpContent;

        _logger.LogDebug("Starting streaming chat to {BaseUrl} (model: {Model}, local: {UseLocal})", baseUrl, model, useLocal);

        using var response = await client.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Streaming chat API error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Chat API error {response.StatusCode}: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            string? token = null;
            try
            {
                var chunk = JsonSerializer.Deserialize<StreamChunkResponse>(data, JsonOptions);
                token = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            }
            catch (JsonException)
            {
                // Skip malformed chunks
            }

            if (!string.IsNullOrEmpty(token))
                yield return token;
        }
    }

    private (string BaseUrl, string Model) GetEndpoint(bool useLocal)
    {
        if (useLocal)
            return (_settings.LocalBaseUrl, _settings.LocalTextModel);
        return (_settings.BaseUrl, _settings.TextModel);
    }

    private HttpClient CreateClient(string baseUrl, bool useLocal)
    {
        var client = _httpClientFactory.CreateClient("MistralChat");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        if (!useLocal)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        return client;
    }

    // Response DTOs (OpenAI-compatible format)
    private class ChatCompletionResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        public MessageContent? Message { get; set; }
    }

    private class MessageContent
    {
        public string? Content { get; set; }
    }

    private class StreamChunkResponse
    {
        public List<StreamChoice>? Choices { get; set; }
    }

    private class StreamChoice
    {
        public DeltaContent? Delta { get; set; }
    }

    private class DeltaContent
    {
        public string? Content { get; set; }
    }
}
