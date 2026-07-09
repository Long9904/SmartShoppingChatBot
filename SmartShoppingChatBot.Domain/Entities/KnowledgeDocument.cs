using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class KnowledgeDocument
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; } 
        public ObjectId BusinessId { get; set; }

        public string Title { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public string PublicId { get; set; } = null!;

        public string ContentType { get; set; } = null!;
        public long SizeInBytes { get; set; }

        public string Type { get; set; } = null!; // e.g., PDF, DOCX, TXT, etc.
        [BsonRepresentation(BsonType.String)]
        public KnowledgeDocumentStatus Status { get; set; }

        public int ChunkCount { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ProcessedAt { get; set; }
    }
}
