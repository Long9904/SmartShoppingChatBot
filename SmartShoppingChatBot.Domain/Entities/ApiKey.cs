using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class ApiKey
    {
        public ObjectId Id { get; set; }

        public ObjectId BusinessId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string KeyId { get; set; } = string.Empty; // Prefix of the key

        public string HashKey { get; set; } = string.Empty; // This is the hashed version of the key, not the plain text key.

        public string EncryptedSecret { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public KeyStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public UserEmbedded? CreatedBy { get; set; }

        public UserEmbedded? UpdatedBy { get; set; }

        public UserEmbedded? RevokedBy { get; set; }
    }
}
