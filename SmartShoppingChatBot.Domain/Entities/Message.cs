using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class Message
    {
        public ObjectId Id { get; set; }

        public ObjectId ConversationId { get; set; }

        public ObjectId BusinessId { get; set; }

        public required string Content { get; set; }

        public SenderTypeEnum SenderType { get; set; }

        public ContentTypeEnum ContentType { get; set; }

        public MessageStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Dictionary<string, string> MetatData { get; set; } = [];
    }
}
