using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class Payment
    {
        [Key]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public ObjectId BussinessId { get; set; }
        public ObjectId SubscriptionPlanId { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public PaymentEnums Status { get; set; } = PaymentEnums.Pending;
        public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
