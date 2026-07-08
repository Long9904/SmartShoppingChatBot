using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class DocumentSection
    {
        public string SectionId { get; set; } = ObjectId.GenerateNewId().ToString();
        public int SectionIndex { get; set; }
        public int Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public string HeadingPath { get; set; } = string.Empty;
        public string MarkdownContent { get; set; } = string.Empty;
        public string SectionSummary { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public int? PageStart { get; set; }
        public int? PageEnd { get; set; }
    }
}
