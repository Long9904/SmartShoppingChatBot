namespace SmartShoppingChatBot.Application.DTOs
{
    public class ConversationContextCache
    {
        public string ConversationId { get; init; } = string.Empty;

        // Tóm tắt lũy tiến toàn bộ hội thoại đến lượt gần nhất.
        public string Summary { get; set; } = string.Empty;

        // Tối đa 8 turn gần nhất.
        // Mỗi turn là 1 cạp user - assitant

        public List<CachedConversationTurn> RecentTurns { get; set; } = [];
    }

    public class CachedConversationTurn
    {
        public string TurnId { get; init; } = string.Empty;

        // User message
        public CachedUserMessage UserMessage { get; init; } = default!;

        // Assitant message
        public CachedAssistantMessage? AssistantMessage { get; set; }
    }

    public sealed class CachedUserMessage
    {
        public string MessageId { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }


    public sealed class CachedAssistantMessage
    {
        public string MessageId { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public List<CachedProductReference> ProductReferences { get; init; } = [];
    }


    public sealed class CachedProductReference
    {
        public string ProductId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
}
