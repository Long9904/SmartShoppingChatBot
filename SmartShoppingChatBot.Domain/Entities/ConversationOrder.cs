using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities;

public sealed class ConversationOrder
{
    public ObjectId Id { get; set; }

    public ObjectId BusinessId { get; set; }

    public ObjectId ConversationId { get; set; }

    public required string ExternalOrderId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ConversationOrderEventStatus Status { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ProductOrderSnapshot> ProductOrderSnapshotItems { get; set; } = [];
}
