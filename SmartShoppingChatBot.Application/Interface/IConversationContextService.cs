using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IConversationContextService
    {
        Task<ConversationContextCache> GetOrLoadAsyncConversationCache(string conversationId, CancellationToken ct);
        Task SaveConversationCacheAsync(ConversationContextCache conversationContextCache, CancellationToken ct);

        Task InvalidateConversationCacheAsync(string conversationId, CancellationToken ct);


    }
}
