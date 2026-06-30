using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class UploadedKnowledgeDocResponse
    {
        public string DocumentId { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public KnowledgeDocumentStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

