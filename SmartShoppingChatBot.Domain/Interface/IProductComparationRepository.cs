using SmartShoppingChatBot.Domain.Entities;

using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IProductComparationRepository : IGenericRepository<ProductComparation>
    {
        Task<CursorPage<ProductComparation>> CursorPagingAsync(
            ObjectId businessId,
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null,
            CancellationToken cancellationToken = default);
    }
}
