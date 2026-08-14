using SmartShoppingChatBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class ConversationOrderEventRepository : GenericRepository<ConversationOrderEvent>, IConversationOrderEventRepository
    {
        public ConversationOrderEventRepository(MongoDbContext context) : base(context)
        {
        }

        public async Task<CursorPage<ConversationOrderEvent>> CursorPagingAsync(
            ObjectId businessId,
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null,
            CancellationToken cancellationToken = default)
        {
            limit = Math.Clamp(limit, 1, 100);
            var query = _context.ConversationOrderEvents.Where(item =>
                item.BusinessId == businessId && item.ConversationId == conversationId);

            if (lastId.HasValue)
            {
                query = query.Where(item => item.Id.CompareTo(lastId.Value) < 0);
            }

            var items = await query
                .OrderByDescending(item => item.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);
            var hasMore = items.Count > limit;
            if (hasMore)
            {
                items.RemoveAt(limit);
            }

            return new CursorPage<ConversationOrderEvent>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = items.Count == 0 ? null : items[^1].Id.ToString()
            };
        }
    }
}
