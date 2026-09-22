using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IConversationContextServiceV3
{
    Task<ConversationContextCacheV3> GetOrLoadAsyncConversationCache(string conversationId, CancellationToken ct);
    Task SaveConversationCacheAsync(ConversationContextCacheV3 context, CancellationToken ct);
}
