using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public interface IConversationContextService
    {
        Task<ConversationContextCache> GetOrLoadAsyncConversationCache(string conversationId, CancellationToken ct);
        Task SaveConversationCacheAsync(ConversationContextCache conversationContextCache, CancellationToken ct);

        Task InvalidateConversationCacheAsync(string conversationId, CancellationToken ct);


    }
}
