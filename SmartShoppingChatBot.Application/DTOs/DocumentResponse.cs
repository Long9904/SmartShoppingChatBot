using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class DocumentResponse
    {
        public string EntryId { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string? SectionTitle { get; set; }
        public string? SectionSummary { get; set; }
        public string? HeadingPath { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? PageStart { get; set; }
        public int? PageEnd { get; set; }
        public double Score { get; set; }

    }
    public class DocumentGetResponse
    {
        public string Id { get; set; }
        public string BusinessId { get; set; }

        public string Title { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public string PublicId { get; set; } = null!;

        public string ContentType { get; set; } = null!;
        public long SizeInBytes { get; set; }
        public string? Type { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
