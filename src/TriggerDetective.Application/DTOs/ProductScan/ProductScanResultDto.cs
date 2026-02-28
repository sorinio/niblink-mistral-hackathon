namespace TriggerDetective.Application.DTOs.ProductScan;

public record ProductScanResultDto(
    bool Success,
    SafetyRating OverallRating,
    string? ProductName,
    string Headline,
    string Explanation,
    List<ScannedIngredientDto> Ingredients,
    List<string> Warnings,
    string? RawOcrText,
    string? ErrorMessage = null
);

public record ScannedIngredientDto(
    string Name,
    SafetyRating Rating,
    string? Reason,
    bool IsPersonalTrigger,
    decimal? CorrelationScore,
    string? IodineRisk
);

public enum SafetyRating
{
    Green,
    Yellow,
    Red
}
