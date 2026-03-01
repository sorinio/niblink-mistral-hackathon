using System.Runtime.CompilerServices;
using System.Text;
using TriggerDetective.Application.DTOs.Chat;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Application.Services;

public class ChatService : IChatService
{
    private readonly ICorrelationService _correlationService;
    private readonly INutrientService _nutrientService;
    private readonly IWarningService _warningService;
    private readonly IFoodLogService _foodLogService;
    private readonly ISymptomLogService _symptomLogService;
    private readonly IMistralChatClient _chatClient;

    private const string SystemPromptTemplate = """
        You are Niblink, a meal advisor for people with Hashimoto's thyroiditis.
        You help users make informed food choices based on their personal tracking data.

        YOUR ROLE:
        - Suggest what to eat or avoid based on detected food-symptom patterns
        - Help fill nutrient gaps (especially B12, Iron, Zinc, Selenium for vegan diets)
        - Warn about upcoming risks ("if you eat X, you've historically experienced Y")
        - Explain WHY certain foods are flagged (goitrogens, soy-medication interaction, iodine)

        SAFETY RULES:
        - NEVER give medical diagnoses or prescribe medications
        - ALWAYS recommend consulting a healthcare provider for medical decisions
        - Correlations are statistical patterns, not proven causation — say so
        - Be clear about confidence levels (high/medium/low)
        - Only discuss food, nutrition, symptoms, and health topics

        STYLE & FORMATTING (IMPORTANT — always follow):
        - Use **bold** for key values, nutrient names, and food names
        - Use bullet points (- ) for lists
        - Use ### for section headers when the answer has multiple parts
        - Short paragraphs, concise but actionable (2-4 paragraphs max)
        - If you lack data to answer, say so honestly
        """;

    public ChatService(
        ICorrelationService correlationService,
        INutrientService nutrientService,
        IWarningService warningService,
        IFoodLogService foodLogService,
        ISymptomLogService symptomLogService,
        IMistralChatClient chatClient)
    {
        _correlationService = correlationService;
        _nutrientService = nutrientService;
        _warningService = warningService;
        _foodLogService = foodLogService;
        _symptomLogService = symptomLogService;
        _chatClient = chatClient;
    }

    public async Task<string> SendMessageAsync(
        Guid userId, string message, List<ChatMessageDto>? history,
        string locale, bool useLocal = false)
    {
        var systemPrompt = await BuildSystemPromptAsync(userId, locale);
        var messages = BuildMessages(systemPrompt, history, message);
        return await _chatClient.ChatAsync(messages, 4096, useLocal);
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        Guid userId, string message, List<ChatMessageDto>? history,
        string locale, bool useLocal = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var systemPrompt = await BuildSystemPromptAsync(userId, locale);
        var messages = BuildMessages(systemPrompt, history, message);

        await foreach (var token in _chatClient.StreamChatAsync(messages, 4096, useLocal, cancellationToken))
        {
            yield return token;
        }
    }

    private async Task<string> BuildSystemPromptAsync(Guid userId, string locale)
    {
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

        // Fetch all context in parallel
        var correlationsTask = _correlationService.GetTopCorrelationsAsync(userId, 10);
        var nutrientsTask = _nutrientService.GetWeeklySummaryAsync(userId);
        var warningsTask = _warningService.GetActiveWarningsAsync(userId);
        var foodLogsTask = _foodLogService.GetFoodLogsAsync(userId, threeDaysAgo, DateTime.UtcNow);
        var symptomLogsTask = _symptomLogService.GetSymptomLogsAsync(userId, threeDaysAgo, DateTime.UtcNow);

        await Task.WhenAll(correlationsTask, nutrientsTask, warningsTask, foodLogsTask, symptomLogsTask);

        var correlations = correlationsTask.Result;
        var nutrients = nutrientsTask.Result;
        var warnings = warningsTask.Result;
        var foodLogs = foodLogsTask.Result;
        var symptomLogs = symptomLogsTask.Result;

        var sb = new StringBuilder();
        sb.AppendLine(SystemPromptTemplate);

        // Locale instruction
        if (locale == "de")
            sb.AppendLine("\nIMPORTANT: Respond entirely in German (Deutsch). All text must be in German.");

        // Correlations
        sb.AppendLine("\nUSER'S HEALTH DATA:");
        sb.AppendLine("\n--- Detected Food-Symptom Patterns ---");
        if (correlations.Count > 0)
        {
            foreach (var c in correlations)
            {
                var direction = c.CorrelationScore > 0 ? "increases" : "decreases";
                var strength = Math.Abs(c.CorrelationScore) switch
                {
                    > 0.7m => "strongly",
                    > 0.4m => "moderately",
                    _ => "slightly"
                };
                sb.AppendLine($"- {c.IngredientName} {strength} {direction} {c.SymptomTypeName} " +
                    $"(score: {c.CorrelationScore:F2}, confidence: {c.ConfidenceLabel}, " +
                    $"delay: {c.AverageDelayHours:F0}h, {c.OccurrenceCount} occurrences, {c.CorrelationType})");
            }
        }
        else
        {
            sb.AppendLine("No significant patterns detected yet.");
        }

        // Nutrients
        sb.AppendLine("\n--- Weekly Nutrient Summary (daily avg vs DGE targets) ---");
        if (nutrients?.NutrientsVsTarget != null)
        {
            foreach (var n in nutrients.NutrientsVsTarget)
            {
                var status = n.PercentOfTarget switch
                {
                    >= 100 => "OK",
                    >= 70 => "low",
                    _ => "DEFICIENT"
                };
                sb.AppendLine($"- {n.Name}: {n.Value:F1} {n.Unit} / {n.Target:F1} {n.Unit} ({n.PercentOfTarget:F0}% — {status})");
            }
        }

        // Warnings
        sb.AppendLine("\n--- Active Warnings ---");
        if (warnings.TotalCount > 0)
        {
            foreach (var w in warnings.Warnings.Take(5))
            {
                sb.AppendLine($"- [{w.Type}] {w.Message}");
            }
        }
        else
        {
            sb.AppendLine("No active warnings.");
        }

        // Recent food logs
        sb.AppendLine("\n--- Recent Meals (last 3 days) ---");
        if (foodLogs.Count > 0)
        {
            foreach (var f in foodLogs.Take(10))
            {
                var ingredients = f.Ingredients.Any()
                    ? string.Join(", ", f.Ingredients.Select(i => i.IngredientName))
                    : "no ingredients logged";
                sb.AppendLine($"- {f.LoggedAt:MMM dd HH:mm} ({f.MealType}): {f.Description} [{ingredients}]");
            }
        }
        else
        {
            sb.AppendLine("No meals logged in the last 3 days.");
        }

        // Recent symptoms
        sb.AppendLine("\n--- Recent Symptoms (last 3 days) ---");
        if (symptomLogs.Count > 0)
        {
            foreach (var s in symptomLogs.Take(10))
            {
                var prefix = s.IsPositive ? "+" : "-";
                sb.AppendLine($"- {s.LoggedAt:MMM dd HH:mm}: {prefix}{s.SymptomTypeName} (severity: {s.Severity}/10)");
            }
        }
        else
        {
            sb.AppendLine("No symptoms logged in the last 3 days.");
        }

        return sb.ToString();
    }

    private static List<object> BuildMessages(string systemPrompt, List<ChatMessageDto>? history, string userMessage)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (history != null)
        {
            foreach (var msg in history)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = userMessage });
        return messages;
    }
}
