using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class KnowledgeEntry
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public ObjectId BusinessId { get; set; }
        public ObjectId DocumentId { get; set; }

        public string QdrantPointId { get; set; } = default!;

        public int ChunkIndex { get; set; }
        public string? SectionId { get; set; }
        public int? SectionIndex { get; set; }
        public string? SectionTitle { get; set; }
        public string? SectionSummary { get; set; }

        public string Content { get; set; } = default!;
        public string ContextualContent { get; set; } = default!;
        public string EmbeddingText { get; set; } = default!;

        public string? HeadingPath { get; set; }
        public int? TokenCount { get; set; }
        public int? PageStart { get; set; }
        public int? PageEnd { get; set; }

        public string FileName { get; set; } = default!;
        public string SourceType { get; set; } = "knowledge_document";

        public DateTime CreatedAt { get; set; }
    }
}
