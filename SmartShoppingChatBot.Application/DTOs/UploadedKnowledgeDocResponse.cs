using SmartShoppingChatBot.Domain.Enums;

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

