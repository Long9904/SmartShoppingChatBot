using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class Token
    {
        [Key]
        public ObjectId Id { get; set; }
        public ObjectId UserId { get; set; }

        public string TokenValue { get; set; } = null!;
        [BsonRepresentation(BsonType.String)]
        public TokenType Type { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
