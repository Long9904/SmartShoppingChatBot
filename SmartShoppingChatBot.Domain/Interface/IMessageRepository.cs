using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IMessageRepository : IGenericRepository<Message>
    {
        Task<CursorPage<Message>> MessageCursorPaging(
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null);
    }
}
