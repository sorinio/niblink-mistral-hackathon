namespace TriggerDetective.Infrastructure.AI;

public class MistralSettings
{
    public const string SectionName = "AI:Mistral";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mistral.ai";
    public string TextModel { get; set; } = "mistral-large-latest";
    public string VisionModel { get; set; } = "pixtral-large-latest";
    public int MaxTokens { get; set; } = 2048;
    public int TimeoutSeconds { get; set; } = 60;
}
