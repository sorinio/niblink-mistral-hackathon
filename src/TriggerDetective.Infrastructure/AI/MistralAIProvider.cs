using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TriggerDetective.Application.DTOs.AI;
using TriggerDetective.Application.DTOs.Insight;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Infrastructure.AI;

public class MistralAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly MistralSettings _settings;
    private readonly ILogger<MistralAIProvider> _logger;

    private const string ExtractionPrompt = """
        You are a food ingredient extraction assistant. Analyze the following meal description and extract all ingredients.

        For each ingredient, provide:
        - name: ONLY the ingredient name (lowercase, singular form, NO quantities - e.g., "rice" not "1 cup rice")
        - confidence: how confident you are this ingredient is present (0.0 to 1.0)
        - category: the food category (vegetable, fruit, grain, legume, dairy, protein, spice, oil, other)
        - quantity: the quantity as a SEPARATE field (e.g., "1 cup", "2 tablespoons", or null if not specified)
        - estimatedGrams: estimated portion weight in grams as a number (e.g., 150 for a medium portion of rice, 30 for a tablespoon of oil). Always estimate even if uncertain.

        Important considerations for Hashimoto's/thyroid health:
        - Flag soy products (tofu, tempeh, edamame, soy milk, soy sauce)
        - Flag goitrogens (raw cruciferous vegetables: kale, broccoli, cabbage, cauliflower, brussels sprouts)
        - Flag gluten-containing grains (wheat, barley, rye)
        - Flag nightshades (tomatoes, peppers, eggplant, potatoes)
        - CRITICAL: Flag algae and seaweed as HIGH IODINE RISK (kombu, wakame, nori, kelp, dulse, hijiki, spirulina, chlorella, carrageenan/E407). These are dangerous for Hashimoto's patients.

        Respond ONLY with valid JSON in this exact format:
        {
            "ingredients": [
                {"name": "ingredient name only", "confidence": 0.95, "category": "category", "quantity": "amount or null", "estimatedGrams": 150}
            ]
        }

        Meal description:
        """;

    private const string InsightGenerationPrompt = """
        You are a health insights assistant specialized in Hashimoto's disease and autoimmune thyroid conditions.
        The user follows a vegan diet and tracks their food intake and symptoms.

        Based on the correlation data provided, generate a personalized, actionable insight summary.

        Guidelines:
        - Focus on the strongest correlations (highest score * confidence)
        - Explain correlations in plain, friendly language (e.g., "You seem to experience fatigue more often about 24 hours after eating soy products")
        - Highlight thyroid-specific concerns: soy interference with medication, goitrogens in raw cruciferous vegetables
        - Note any nutrient patterns relevant to vegans with Hashimoto's (B12, Iron, Zinc, Selenium)
        - Be encouraging but honest about uncertainty (mention confidence levels)
        - Suggest actionable experiments (e.g., "Try avoiding X for a week and see if symptoms improve")
        - Keep the summary concise (3-5 bullet points or short paragraphs)
        - Do NOT give medical advice - encourage consulting healthcare providers for significant concerns

        Correlation data:
        """;

    private const string ImageExtractionPrompt = """
        You are a food ingredient extraction assistant. Look at this image of food and:
        1. Describe what dish/meal this appears to be (brief, 3-8 words)
        2. Identify all visible ingredients

        For each ingredient, provide:
        - name: ONLY the ingredient name (lowercase, singular form, NO quantities - e.g., "rice" not "1 cup rice")
        - confidence: how confident you are this ingredient is present (0.0 to 1.0)
        - category: the food category (vegetable, fruit, grain, legume, dairy, protein, spice, oil, other)
        - quantity: estimated quantity as a SEPARATE field (e.g., "1 cup", "a handful", or null)
        - estimatedGrams: estimated portion weight in grams as a number (e.g., 150 for a medium portion of rice, 30 for a tablespoon of oil). Always estimate even if uncertain.

        Important considerations for Hashimoto's/thyroid health:
        - Flag soy products (tofu, tempeh, edamame, soy milk)
        - Flag goitrogens (raw cruciferous vegetables: kale, broccoli, cabbage, cauliflower)
        - Flag nightshades (tomatoes, peppers, eggplant, potatoes)
        - CRITICAL: Flag algae and seaweed as HIGH IODINE RISK (kombu, wakame, nori, kelp, dulse, hijiki, spirulina, chlorella, carrageenan/E407). These are dangerous for Hashimoto's patients.

        Respond ONLY with valid JSON in this exact format:
        {
            "description": "Brief description of the meal",
            "ingredients": [
                {"name": "ingredient name only", "confidence": 0.95, "category": "category", "quantity": "amount or null", "estimatedGrams": 150}
            ]
        }
        """;

    public MistralAIProvider(
        HttpClient httpClient,
        IOptions<MistralSettings> settings,
        ILogger<MistralAIProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    private static string GetLocaleInstruction(string locale) => locale switch
    {
        "de" => "\n\nIMPORTANT: Respond entirely in German (Deutsch). All ingredient names, category names, descriptions, and any text must be in German.",
        _ => ""
    };

    public async Task<IngredientExtractionResult> ExtractIngredientsFromTextAsync(string mealDescription, string locale = "en")
    {
        try
        {
            var localeInstruction = GetLocaleInstruction(locale);
            var request = CreateChatRequest(_settings.TextModel, new object[]
            {
                new { role = "user", content = ExtractionPrompt + localeInstruction + "\n\n" + mealDescription }
            });

            return await SendRequestAndParseIngredients(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting ingredients from text: {Description}", mealDescription);
            return new IngredientExtractionResult(
                [],
                null,
                false,
                $"Failed to extract ingredients: {ex.Message}"
            );
        }
    }

    public async Task<IngredientExtractionResult> ExtractIngredientsFromImageAsync(Stream image, string? hint = null, string locale = "en")
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await image.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);

            var localeInstruction = GetLocaleInstruction(locale);
            var prompt = hint != null
                ? $"{ImageExtractionPrompt}{localeInstruction}\n\nAdditional context: {hint}"
                : $"{ImageExtractionPrompt}{localeInstruction}";

            var contentParts = new object[]
            {
                new
                {
                    type = "image_url",
                    image_url = new { url = $"data:image/jpeg;base64,{base64Image}" }
                },
                new
                {
                    type = "text",
                    text = prompt
                }
            };

            var request = CreateChatRequest(_settings.VisionModel, new object[]
            {
                new { role = "user", content = contentParts }
            });

            return await SendRequestAndParseIngredients(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting ingredients from image");
            return new IngredientExtractionResult(
                [],
                null,
                false,
                $"Failed to extract ingredients from image: {ex.Message}"
            );
        }
    }

    public async Task<InsightGenerationResult> GenerateInsightSummaryAsync(string correlationData, string locale = "en")
    {
        try
        {
            var localeInstruction = GetLocaleInstruction(locale);
            var request = CreateChatRequest(_settings.TextModel, new object[]
            {
                new { role = "user", content = InsightGenerationPrompt + localeInstruction + "\n\n" + correlationData }
            });

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending insight generation request to Mistral API");

            var response = await _httpClient.PostAsync("/v1/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mistral API insight generation error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new InsightGenerationResult(false, null, responseBody, $"API error {response.StatusCode}: {responseBody}");
            }

            var textContent = ExtractTextFromResponse(responseBody);

            if (string.IsNullOrEmpty(textContent))
            {
                return new InsightGenerationResult(false, null, responseBody, "No text content in response");
            }

            return new InsightGenerationResult(true, textContent, responseBody, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating insight summary");
            return new InsightGenerationResult(false, null, null, $"Failed to generate insights: {ex.Message}");
        }
    }

    private object CreateChatRequest(string model, object[] messages)
    {
        return new
        {
            model,
            max_tokens = _settings.MaxTokens,
            messages
        };
    }

    private async Task<IngredientExtractionResult> SendRequestAndParseIngredients(object request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending request to Mistral API");

        var response = await _httpClient.PostAsync("/v1/chat/completions", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Mistral API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            return new IngredientExtractionResult(
                [],
                responseBody,
                false,
                $"API error: {response.StatusCode}"
            );
        }

        var textContent = ExtractTextFromResponse(responseBody);

        if (string.IsNullOrEmpty(textContent))
        {
            return new IngredientExtractionResult(
                [],
                responseBody,
                false,
                "No text content in response"
            );
        }

        var (ingredients, description) = ParseIngredientsFromJson(textContent);

        return new IngredientExtractionResult(
            ingredients,
            responseBody,
            true,
            null,
            description
        );
    }

    private static string? ExtractTextFromResponse(string responseBody)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MistralResponse>(responseBody, options);
        return response?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private (List<ExtractedIngredient> Ingredients, string? Description) ParseIngredientsFromJson(string jsonText)
    {
        try
        {
            // Strip markdown code blocks if present
            jsonText = jsonText.Trim();
            if (jsonText.StartsWith("```json"))
                jsonText = jsonText[7..];
            if (jsonText.StartsWith("```"))
                jsonText = jsonText[3..];
            if (jsonText.EndsWith("```"))
                jsonText = jsonText[..^3];
            jsonText = jsonText.Trim();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            ExtractionResponse? result = null;
            try
            {
                result = JsonSerializer.Deserialize<ExtractionResponse>(jsonText, options);
            }
            catch (JsonException)
            {
                // Truncated JSON from max_tokens — try to salvage by closing brackets
                result = TryRepairTruncatedJson(jsonText, options);
                if (result != null)
                    _logger.LogWarning("Recovered {Count} ingredients from truncated JSON response", result.Ingredients?.Count ?? 0);
            }

            var ingredients = result?.Ingredients?.Select(i => new ExtractedIngredient(
                i.Name ?? "unknown",
                (decimal)(i.Confidence ?? 0.5),
                null,
                i.Category,
                i.Quantity,
                i.EstimatedGrams.HasValue ? (decimal)i.EstimatedGrams.Value : null
            )).ToList() ?? [];

            return (ingredients, result?.Description);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse ingredients JSON: {Text}", jsonText);
            return ([], null);
        }
    }

    private ExtractionResponse? TryRepairTruncatedJson(string jsonText, JsonSerializerOptions options)
    {
        // Remove any trailing incomplete object (e.g. `{"name": "apfel` or `{"name": "apfel",`)
        var lastCompleteObject = jsonText.LastIndexOf('}');
        if (lastCompleteObject < 0) return null;

        var repaired = jsonText[..(lastCompleteObject + 1)];

        // Close any unclosed array/object brackets
        var openBraces = repaired.Count(c => c == '{') - repaired.Count(c => c == '}');
        var openBrackets = repaired.Count(c => c == '[') - repaired.Count(c => c == ']');

        repaired += new string(']', Math.Max(0, openBrackets));
        repaired += new string('}', Math.Max(0, openBraces));

        try
        {
            return JsonSerializer.Deserialize<ExtractionResponse>(repaired, options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not repair truncated JSON");
            return null;
        }
    }

    // Mistral API response DTOs (OpenAI-compatible format)
    private class MistralResponse
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

    private class ExtractionResponse
    {
        public string? Description { get; set; }
        public List<IngredientItem>? Ingredients { get; set; }
    }

    private class IngredientItem
    {
        public string? Name { get; set; }
        public double? Confidence { get; set; }
        public string? Category { get; set; }
        public string? Quantity { get; set; }
        public double? EstimatedGrams { get; set; }
    }
}
