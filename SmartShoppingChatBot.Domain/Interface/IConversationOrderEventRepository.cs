using SmartShoppingChatBot.Domain.Entities;

using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IConversationOrderEventRepository : IGenericRepository<ConversationOrderEvent>
    {
        Task<CursorPage<ConversationOrderEvent>> CursorPagingAsync(
            ObjectId businessId,
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null,
            CancellationToken cancellationToken = default);
    }
}
