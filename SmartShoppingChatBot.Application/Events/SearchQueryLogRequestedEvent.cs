namespace SmartShoppingChatBot.Application.Events;

public record SearchQueryLogRequestedEvent
{
    public required string BusinessId { get; init; }

    public required string ConversationId { get; init; }

    public required string MessageId { get; init; }

    public required string UserRawQuery { get; init; }

    public List<string>? TrendKeywords { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public long RetrievalLatency { get; init; }

    public int TopKResult { get; init; }

    public List<SearchQueryProductSnapshot> ProductResults { get; init; } = [];
}

public record SearchQueryProductSnapshot
{
    public required string ProductId { get; init; }

    public string? ProductName { get; init; }

    public decimal Price { get; init; }

    public string? Category { get; init; }

    public double ProductScore { get; init; }
}
