using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class UsageQuotaLog
    {
        public ObjectId Id { get; set; }

        public ObjectId BusinessId { get; set; }

        public ObjectId BusinessQuotaId { get; set; }

        public ObjectId SourceId { get; set; }

        [BsonRepresentation(BsonType.String)]
        public SourceTypeEnum SourceType { get; set; }

        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }

        // Token quy đổi đã trừ
        public long BillableTokens { get; set; }

        public int MessageUsed { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
