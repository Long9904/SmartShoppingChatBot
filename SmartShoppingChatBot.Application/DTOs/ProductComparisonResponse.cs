using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed record ProductComparisonResponse
{
    public required string Id { get; init; }
    public required string MessageId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<ProductSnapshotResponse> Products { get; init; } = [];

    public static ProductComparisonResponse FromEntity(ProductComparation entity) => new()
    {
        Id = entity.Id.ToString(),
        MessageId = entity.MessageId.ToString(),
        CreatedAt = entity.CreatedAt,
        Title = entity.Title,
        Summary = entity.Summary,
        Products = entity.RecommendationObjects.Select(product => new ProductSnapshotResponse
        {
            ProductId = product.ProductId.ToString(),
            ProductName = product.ProductName,
            Price = product.Price,
            Category = product.Category
        }).ToList()
    };
}

public sealed record ProductSnapshotResponse
{
    public required string ProductId { get; init; }
    public string? ProductName { get; init; }
    public decimal Price { get; init; }
    public string? Category { get; init; }
}
