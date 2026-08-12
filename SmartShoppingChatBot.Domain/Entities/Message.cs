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

        public string? SummaryContent { get; set; } = default!;

        public SenderTypeEnum SenderType { get; set; }

        public ContentTypeEnum ContentType { get; set; }

        public MessageStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Dictionary<string, string> MetaData { get; set; } = [];

        public List<ProductReference> CacheProductReference { get; set; } = new List<ProductReference>();
    }


    public class ProductReference
    {
        public string ProductId { get; init; } = string.Empty;
        public string? ExternalProductId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
    }
}
