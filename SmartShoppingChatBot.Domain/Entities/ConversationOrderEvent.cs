using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class ConversationOrderEvent
    {
        public ObjectId Id { get; set; }

        public ObjectId BusinessId { get; set; }

        public ObjectId ConversationId { get; set; }

        public string? ExternalOrderId { get; set; }

        [BsonRepresentation(BsonType.String)]
        public ConversationOrderEventStatus Status { get; set; }

        public decimal Amount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<ProductOrderSnapshot> ProductOrderSnapshotItems { get; set; } = [];
    }

    public class ProductOrderSnapshot
    {
        public required string ExternalProductId { get; set; }

        public string? ProductName { get; set; }

        public decimal Price { get; set; }

        public int? Quantity { get; set; }
    }
}
