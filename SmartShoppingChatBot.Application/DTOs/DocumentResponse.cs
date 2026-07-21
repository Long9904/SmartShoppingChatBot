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
}
