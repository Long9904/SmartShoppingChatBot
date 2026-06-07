using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class User
    {
        [Key]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool MustChangePassword { get; set; }
        public bool IsProfileCompleted { get; set; }
        public string? PasswordHash { get; set; }
        public string? PhoneNumber { get; set; }

        [BsonRepresentation(BsonType.String)]
        public RoleEnums Role { get; set; }

        [BsonRepresentation(BsonType.String)]
        public UserStatus UserStatus { get; set; }

        // Business snapshot
        public ObjectId BusinessId { get; set; }
        public string? BusinessName { get; set; }

        // Audit fields
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedAt { get; set; }
        public UserEmbedded? CreatedBy { get; set; }
        public UserEmbedded? UpdatedBy { get; set; }
    }
}
