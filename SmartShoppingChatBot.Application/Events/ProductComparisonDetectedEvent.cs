namespace SmartShoppingChatBot.Application.Events;

public sealed record ProductComparisonDetectedEvent
{
    public required string BusinessId { get; init; }

    public required string ConversationId { get; init; }

    public required string MessageId { get; init; }

    public required string CustomerId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public List<ComparedProductSnapshot> Products { get; init; } = [];
}

public sealed record ComparedProductSnapshot
{
    public required string ProductId { get; init; }

    public string? ProductName { get; init; }

    public decimal Price { get; init; }

    public string? Category { get; init; }
}
