using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed record SearchQueryLogResponse
{
    public required string Id { get; init; }
    public required string MessageId { get; init; }
    public string? UserRawQuery { get; init; }
    public IReadOnlyList<string>? TrendKeywords { get; init; }
    public bool ZeroResult { get; init; }
    public int ResultCount { get; init; }
    public int TopKResult { get; init; }
    public double HitRateScore { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long RetrievalLatencyMilliseconds { get; init; }
    public IReadOnlyList<ProductLogSnapshotResponse> Products { get; init; } = [];

    public static SearchQueryLogResponse FromEntity(SearchQueryLog entity) => new()
    {
        Id = entity.Id.ToString(),
        MessageId = entity.MessageId.ToString(),
        UserRawQuery = entity.UserRawQuery,
        TrendKeywords = entity.TrendKeywords,
        ZeroResult = entity.ZeroResult,
        ResultCount = entity.ResultCountNumber,
        TopKResult = entity.TopKResult,
        HitRateScore = entity.HitRateScore ?? 0,
        CreatedAt = entity.CreatedAt,
        RetrievalLatencyMilliseconds = entity.RetrievalLatency,
        Products = entity.ProductResults.Select(product => new ProductLogSnapshotResponse
        {
            ProductId = product.ProductId.ToString(),
            ProductName = product.ProductName,
            Price = product.Price,
            Category = product.Category,
            ProductScore = product.ProductScore
        }).ToList()
    };
}

public sealed record ProductLogSnapshotResponse
{
    public required string ProductId { get; init; }
    public string? ProductName { get; init; }
    public decimal Price { get; init; }
    public string? Category { get; init; }
    public double ProductScore { get; init; }
}
