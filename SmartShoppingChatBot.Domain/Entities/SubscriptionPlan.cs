using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class SubscriptionPlan
    {
        [Key]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        // Duration (in days) for the subscription plan
        public int Duration { get; set; }
        public int Level { get; set; } = 0;
        [BsonRepresentation(BsonType.String)]
        public StatusEnums Status { get; set; } = StatusEnums.Active;
        //Monthly token limit for the subscription plan
        public long TokenLimit { get; set; } = 0;
        public int MessageLimit { get; set; } = 0;
        public int MaxProductAllowed { get; set; } = 0;
        public int MaxDocumentAllowed { get; set; } = 0;

    }
}
