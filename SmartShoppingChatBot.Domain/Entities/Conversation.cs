using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class Conversation
    {
        public ObjectId Id { get; set; }

        public ObjectId BusinessId { get; set; }

        public ObjectId CustomerId { get; set; }

        public required string Title { get; set; }

        public ConversationStatus Status { get; set; }

        public DateTimeOffset LastMessageAt { get; set; }

        public DateTimeOffset CreateAt { get; set; }

        public string? Summary { get; set; } = string.Empty;

        public DateTimeOffset? SummaryUpdatedAt { get; set; }

        public Dictionary<string, string> MetaData { get; set; } = [];
    }
}
