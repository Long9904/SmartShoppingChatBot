namespace SmartShoppingChatBot.Application.DTOs
{
    public class MarkdownChunkResult
    {
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = default!;
        public string ContextualContent { get; set; } = default!;
        public string EmbeddingText { get; set; } = default!;
        public string? HeadingPath { get; set; }
        public int? TokenCount { get; set; }
    }
}
