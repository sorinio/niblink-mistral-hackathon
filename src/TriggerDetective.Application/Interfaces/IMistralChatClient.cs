namespace TriggerDetective.Application.Interfaces;

public interface IMistralChatClient
{
    Task<string> ChatAsync(List<object> messages, int maxTokens = 2048, bool useLocal = false);
    IAsyncEnumerable<string> StreamChatAsync(List<object> messages, int maxTokens = 2048, bool useLocal = false, CancellationToken cancellationToken = default);
}
