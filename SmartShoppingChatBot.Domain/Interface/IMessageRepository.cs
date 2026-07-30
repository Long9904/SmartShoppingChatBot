using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IMessageRepository : IGenericRepository<Message>
    {
        Task<CursorPage<Message>> MessageCursorPaging(
            ObjectId conversationId,
            int limit,
            ObjectId? lastId = null,
            string? search = null,
            SenderTypeEnum? senderType = null);
    }
}
