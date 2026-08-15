namespace SmartShoppingChatBot.Application.DTOs;

public sealed class BusinessDashboardResponse
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }

    public int TotalProducts { get; init; }
    public int TotalKnowledgeDocuments { get; init; }
    public int TotalChatSessions { get; init; }
    public int TotalChatMessages { get; init; }
    public int SuccessfulOrderConversations { get; init; }
    public double ConversionRate { get; init; }
    public double? AverageRetrievalLatencyMilliseconds { get; init; }
    public double? AverageSearchHitRatePercentage { get; init; }

    public IReadOnlyList<BusinessDashboardTrafficPoint> ChatTraffic { get; init; } = [];
    public IReadOnlyList<BusinessDashboardZeroResultQuery> ZeroResultQueries { get; init; } = [];
    public IReadOnlyList<BusinessDashboardIntent> Intents { get; init; } = [];
    public IReadOnlyList<BusinessDashboardKeyword> TrendingKeywords { get; init; } = [];
}

public sealed class BusinessDashboardTrafficPoint
{
    public DateOnly Date { get; init; }
    public int Sessions { get; init; }
    public int Messages { get; init; }
}

public sealed class BusinessDashboardZeroResultQuery
{
    public required string Query { get; init; }
    public int Count { get; init; }
    public DateTimeOffset LastOccurredAt { get; init; }
}

public sealed class BusinessDashboardIntent
{
    public required string Intent { get; init; }
    public int Count { get; init; }
}

public sealed class BusinessDashboardKeyword
{
    public required string Keyword { get; init; }
    public int Count { get; init; }
}
