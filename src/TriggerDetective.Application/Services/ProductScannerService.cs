using Microsoft.Extensions.Logging;
using TriggerDetective.Application.DTOs.ProductScan;
using TriggerDetective.Application.Interfaces;
using TriggerDetective.Domain.Helpers;

namespace TriggerDetective.Application.Services;

public class ProductScannerService : IProductScannerService
{
    private readonly IAIProvider _aiProvider;
    private readonly IBlsFoodService _blsFoodService;
    private readonly ICorrelationService _correlationService;
    private readonly ILogger<ProductScannerService> _logger;

    // Common E-numbers that indicate hidden soy or iodine sources
    private static readonly Dictionary<string, (string Name, SafetyRating Rating, string Reason)> ENumberRisks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["e322"] = ("Lecithin", SafetyRating.Yellow, "Often soy-derived (soy lecithin)"),
        ["e407"] = ("Carrageenan", SafetyRating.Red, "Seaweed-derived — high iodine risk"),
        ["e407a"] = ("Carrageenan", SafetyRating.Red, "Seaweed-derived — high iodine risk"),
        ["e401"] = ("Sodium alginate", SafetyRating.Red, "Algae-derived — high iodine risk"),
        ["e402"] = ("Potassium alginate", SafetyRating.Red, "Algae-derived — high iodine risk"),
        ["e403"] = ("Ammonium alginate", SafetyRating.Red, "Algae-derived — high iodine risk"),
        ["e404"] = ("Calcium alginate", SafetyRating.Red, "Algae-derived — high iodine risk"),
        ["e405"] = ("Propylene glycol alginate", SafetyRating.Red, "Algae-derived — high iodine risk"),
        ["e426"] = ("Soybean hemicellulose", SafetyRating.Yellow, "Soy-derived"),
    };

    // Known soy indicators (lowercase)
    private static readonly HashSet<string> SoyKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "soy", "soja", "soybean", "sojabean", "tofu", "tempeh", "edamame",
        "soy lecithin", "sojalecithin", "soy protein", "sojaprotein",
        "soy sauce", "sojasauce", "soy milk", "sojamilch", "sojabohne"
    };

    // Known goitrogen indicators (raw cruciferous)
    private static readonly HashSet<string> GoitrogenKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "kale", "grünkohl", "broccoli", "brokkoli", "cabbage", "kohl",
        "cauliflower", "blumenkohl", "brussels sprout", "rosenkohl",
        "bok choy", "pak choi", "kohlrabi", "radish", "rettich", "meerrettich"
    };

    public ProductScannerService(
        IAIProvider aiProvider,
        IBlsFoodService blsFoodService,
        ICorrelationService correlationService,
        ILogger<ProductScannerService> logger)
    {
        _aiProvider = aiProvider;
        _blsFoodService = blsFoodService;
        _correlationService = correlationService;
        _logger = logger;
    }

    public async Task<ProductScanResultDto> ScanProductLabelAsync(Guid userId, Stream labelPhoto, string locale = "en")
    {
        try
        {
            // Step 1 + 2: Run AI extraction and personal trigger lookup in parallel
            var labelHint = locale == "de"
                ? "Dies ist ein Foto einer Produktzutatenliste. Lies den Text auf dem Etikett und extrahiere jede einzelne Zutat."
                : "This is a photo of a product ingredient label (Zutatenliste). Read the text on the label and extract every ingredient listed.";

            var extractionTask = _aiProvider.ExtractIngredientsFromImageAsync(labelPhoto, labelHint, locale);
            var correlationsTask = _correlationService.GetTopCorrelationsAsync(userId, 20);

            await Task.WhenAll(extractionTask, correlationsTask);

            var extractionResult = extractionTask.Result;
            var topCorrelations = correlationsTask.Result;

            if (!extractionResult.Success || extractionResult.Ingredients.Count == 0)
            {
                return new ProductScanResultDto(
                    false, SafetyRating.Green, null,
                    locale == "de" ? "Zutatenliste konnte nicht gelesen werden" : "Could not read ingredient list",
                    locale == "de" ? "Versuche es mit besserer Beleuchtung oder einem klareren Foto." : "Try better lighting or a clearer photo.",
                    [], [], null,
                    extractionResult.ErrorMessage ?? "No ingredients extracted from label"
                );
            }

            var personalTriggers = topCorrelations
                .Where(c => c.CorrelationScore > 0.4m && c.ConfidenceLabel is "high" or "medium")
                .ToDictionary(c => c.IngredientName.ToLowerInvariant(), c => c);

            // Step 3: Analyze each extracted ingredient
            var scannedIngredients = new List<ScannedIngredientDto>();
            var warnings = new List<string>();

            foreach (var ingredient in extractionResult.Ingredients)
            {
                var name = ingredient.Name.ToLowerInvariant().Trim();
                var rating = SafetyRating.Green;
                string? reason = null;
                var isPersonalTrigger = false;
                decimal? correlationScore = null;
                string? iodineRisk = null;

                // Check E-numbers
                var eNumberMatch = ENumberRisks.Keys.FirstOrDefault(e => name.Contains(e, StringComparison.OrdinalIgnoreCase));
                if (eNumberMatch != null)
                {
                    var risk = ENumberRisks[eNumberMatch];
                    rating = MaxRating(rating, risk.Rating);
                    reason = risk.Reason;
                }

                // Check iodine risk (algae/seaweed)
                var iodineLevel = IodineRiskCalculator.Classify(name);
                iodineRisk = iodineLevel.ToString();
                if (iodineLevel == Domain.Enums.IodineRiskLevel.Red)
                {
                    rating = SafetyRating.Red;
                    reason = locale == "de" ? "Hoher Jodgehalt — gefährlich bei Hashimoto" : "High iodine — dangerous for Hashimoto's";
                    warnings.Add(locale == "de"
                        ? $"WARNUNG: {ingredient.Name} enthält hohes Jod (Algen/Seetang)"
                        : $"WARNING: {ingredient.Name} is high in iodine (algae/seaweed)");
                }
                else if (iodineLevel == Domain.Enums.IodineRiskLevel.Yellow)
                {
                    rating = MaxRating(rating, SafetyRating.Yellow);
                    reason ??= locale == "de" ? "Verarbeitetes Produkt — möglicherweise jodiertes Salz" : "Processed food — may contain iodized salt";
                }

                // Check soy content
                if (SoyKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    rating = MaxRating(rating, SafetyRating.Red);
                    reason = locale == "de"
                        ? "Enthält Soja — kann Schilddrüsenmedikament-Aufnahme blockieren"
                        : "Contains soy — can block thyroid medication absorption";
                    warnings.Add(locale == "de"
                        ? $"Soja erkannt: {ingredient.Name} — 4+ Stunden Abstand zur Medikation einhalten"
                        : $"Soy detected: {ingredient.Name} — maintain 4+ hour gap from medication");
                }

                // Check goitrogens
                if (GoitrogenKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    rating = MaxRating(rating, SafetyRating.Yellow);
                    reason ??= locale == "de"
                        ? "Goitrogen — Kochen reduziert die Wirkung um 90%"
                        : "Goitrogen — cooking reduces effect by 90%";
                }

                // Check personal triggers (skip BLS fuzzy matching entirely if user has none)
                if (personalTriggers.Count > 0)
                {
                    if (personalTriggers.TryGetValue(name, out var trigger))
                    {
                        isPersonalTrigger = true;
                        correlationScore = trigger.CorrelationScore;
                        rating = SafetyRating.Red;
                        reason = locale == "de"
                            ? $"Persönlicher Trigger: korreliert mit {trigger.SymptomTypeName} ({trigger.ConfidenceLabel})"
                            : $"Personal trigger: correlates with {trigger.SymptomTypeName} ({trigger.ConfidenceLabel})";
                    }
                    else
                    {
                        // BLS fuzzy match for broader personal trigger matching
                        var blsMatches = await _blsFoodService.FuzzyMatchAsync(name, locale, 1);
                        if (blsMatches.Count > 0 && blsMatches[0].MatchScore >= 0.7m)
                        {
                            var blsFoodName = blsMatches[0].BlsFood.NameEn.ToLowerInvariant();
                            var blsFoodNameDe = blsMatches[0].BlsFood.NameDe.ToLowerInvariant();

                            var fuzzyTrigger = personalTriggers.Keys
                                .FirstOrDefault(t => blsFoodName.Contains(t) || blsFoodNameDe.Contains(t) || t.Contains(blsFoodName) || t.Contains(blsFoodNameDe));

                            if (fuzzyTrigger != null)
                            {
                                var matchedTrigger = personalTriggers[fuzzyTrigger];
                                isPersonalTrigger = true;
                                correlationScore = matchedTrigger.CorrelationScore;
                                rating = SafetyRating.Red;
                                reason = locale == "de"
                                    ? $"Persönlicher Trigger: korreliert mit {matchedTrigger.SymptomTypeName} ({matchedTrigger.ConfidenceLabel})"
                                    : $"Personal trigger: correlates with {matchedTrigger.SymptomTypeName} ({matchedTrigger.ConfidenceLabel})";
                            }
                        }
                    }
                }

                scannedIngredients.Add(new ScannedIngredientDto(
                    ingredient.Name,
                    rating,
                    reason,
                    isPersonalTrigger,
                    correlationScore,
                    iodineRisk
                ));
            }

            // Step 4: Calculate overall rating (worst ingredient wins)
            var overallRating = scannedIngredients.Max(i => i.Rating);

            // Step 5: Generate headline and explanation
            var (headline, explanation) = GenerateVerdict(overallRating, scannedIngredients, warnings, locale);

            return new ProductScanResultDto(
                true,
                overallRating,
                extractionResult.Description,
                headline,
                explanation,
                scannedIngredients,
                warnings,
                extractionResult.RawResponse
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning product label");
            return new ProductScanResultDto(
                false, SafetyRating.Green, null,
                "Scan failed", ex.Message,
                [], [], null, ex.Message
            );
        }
    }

    private static SafetyRating MaxRating(SafetyRating current, SafetyRating candidate)
        => candidate > current ? candidate : current;

    private static (string Headline, string Explanation) GenerateVerdict(
        SafetyRating rating, List<ScannedIngredientDto> ingredients, List<string> warnings, string locale)
    {
        var redCount = ingredients.Count(i => i.Rating == SafetyRating.Red);
        var yellowCount = ingredients.Count(i => i.Rating == SafetyRating.Yellow);
        var triggerCount = ingredients.Count(i => i.IsPersonalTrigger);

        return (rating, locale) switch
        {
            (SafetyRating.Red, "de") => (
                "Dieses Produkt vermeiden",
                triggerCount > 0
                    ? $"{triggerCount} persönliche(r) Trigger und {redCount} Risikozutat(en) erkannt."
                    : $"{redCount} Risikozutat(en) für Hashimoto erkannt."
            ),
            (SafetyRating.Red, _) => (
                "Avoid this product",
                triggerCount > 0
                    ? $"{triggerCount} personal trigger(s) and {redCount} risk ingredient(s) detected."
                    : $"{redCount} risk ingredient(s) detected for Hashimoto's."
            ),
            (SafetyRating.Yellow, "de") => (
                "Vorsicht empfohlen",
                $"{yellowCount} Zutat(en) erfordern Aufmerksamkeit."
            ),
            (SafetyRating.Yellow, _) => (
                "Caution advised",
                $"{yellowCount} ingredient(s) require attention."
            ),
            (_, "de") => (
                "Sicher zum Verzehr",
                "Keine bekannten Risiken für Hashimoto erkannt."
            ),
            _ => (
                "Safe to eat",
                "No known Hashimoto's risks detected."
            )
        };
    }
}
