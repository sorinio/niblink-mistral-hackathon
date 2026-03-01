namespace TriggerDetective.Application.DTOs.Chat;

public record ChatMessageDto(string Role, string Content);

public record ChatRequest(
    string Message,
    List<ChatMessageDto>? History = null,
    string Locale = "en",
    bool UseLocal = false
);

public record ChatResponse(string Message);
