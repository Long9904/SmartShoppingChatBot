using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class User
    {
        [Key]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public required string Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsProfileCompleted { get; set; }
        public required string PasswordHash { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Gender { get; set; }
        public DateTimeOffset? EmailVerifiedAt { get; set; }

        [BsonRepresentation(BsonType.String)]
        public UserStatus UserStatus { get; set; }

        public required BusinessEmbedded Business { get; set; }

        // Audit fields
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public UserEmbedded? CreatedBy { get; set; }
        public UserEmbedded? UpdatedBy { get; set; }
    }


    public class BusinessEmbedded
    {
        public ObjectId Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public RoleEnums Role { get; set; }
        public string? BusinessName { get; set; } = null;
        public DateTimeOffset JoinedAt { get; set; }
    }
}
