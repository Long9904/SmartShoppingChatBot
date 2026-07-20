using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IConversationContextCacheService
    {
        Task<ConversationContextCache?> GetAsync(
            string conversationId,
            CancellationToken cancellationToken = default);

        Task SetAsync(
            ConversationContextCache context,
            CancellationToken cancellationToken = default);

        Task<bool> RefreshExpirationAsync(
            string conversationId,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            string conversationId,
            CancellationToken cancellationToken = default);
    }
}
