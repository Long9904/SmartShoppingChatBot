using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class MessageRepository : GenericRepository<Message>, IMessageRepository
    {
        public MessageRepository(MongoDbContext context) : base(context)
        {
        }

        public async Task<CursorPage<Message>> MessageCursorPaging(
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null,
            string? search = null,
            SenderTypeEnum? senderType = null)
        {
            limit = limit < 1 ? 1 : limit;
            var query = _context.Messages
                 .Where(x => x.ConversationId == conversationId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.Content.Contains(search));
            }

            if (senderType.HasValue)
            {
                query = query.Where(x => x.SenderType == senderType.Value);
            }

            if (lastId.HasValue)
            {
                query = query.Where(x => x.Id.CompareTo(lastId.Value) < 0);
            }

            var items = await query
               .OrderByDescending(x => x.Id)
               .Take(limit + 1)
               .ToListAsync();

            var hasMore = items.Count > limit;

            if (hasMore)
            {
                items.RemoveAt(limit);
            }

            ObjectId? nextCursor = null;

            if (items.Count > 0)
            {
                nextCursor = items[^1].Id;
            }

            return new CursorPage<Message>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = nextCursor.ToString(),
            };

        }
    }
}
