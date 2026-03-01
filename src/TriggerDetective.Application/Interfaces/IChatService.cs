using TriggerDetective.Application.DTOs.Chat;

namespace TriggerDetective.Application.Interfaces;

public interface IChatService
{
    Task<string> SendMessageAsync(Guid userId, string message, List<ChatMessageDto>? history, string locale, bool useLocal = false);
    IAsyncEnumerable<string> StreamMessageAsync(Guid userId, string message, List<ChatMessageDto>? history, string locale, bool useLocal = false, CancellationToken cancellationToken = default);
}
