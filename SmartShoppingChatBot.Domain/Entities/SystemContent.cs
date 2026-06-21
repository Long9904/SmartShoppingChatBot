using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class SystemContent
    {
        [Key]
        public ObjectId Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public ContentType ContentType { get; set; } = ContentType.Markdown;

        public int Version { get; set; } = 1;

        [BsonRepresentation(BsonType.String)]
        public SystemContentStatus Status { get; set; } = SystemContentStatus.Draft;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public UserEmbedded? CreatedBy { get; set; }

        public UserEmbedded? UpdatedBy { get; set; }
    }
}
