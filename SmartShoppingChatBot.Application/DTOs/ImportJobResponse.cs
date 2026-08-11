using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ImportJobResponse
    {
        public string? Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;

        public int TotalRows { get; set; }

        public int ProcessedRows { get; set; }

        public int SuccessRows { get; set; }

        public int FailedRows { get; set; }

        public int EmbeddedRows { get; set; }

        public List<ImportRowError> Errors { get; set; } = [];

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? StartedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }
    }
}
