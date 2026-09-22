namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ConversationContextCacheV3
{
    public string ConversationId { get; init; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<CachedConversationTurnV3> RecentTurns { get; set; } = [];
}

public sealed class CachedConversationTurnV3
{
    public string TurnId { get; init; } = string.Empty;
    public CachedUserMessageV3 UserMessage { get; init; } = new();
    public CachedAssistantMessageV3? AssistantMessage { get; set; }
}

public sealed class CachedUserMessageV3
{
    public string MessageId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class CachedAssistantMessageV3
{
    public string MessageId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public List<ProductReferenceV3> ProductReferences { get; init; } = [];
}
