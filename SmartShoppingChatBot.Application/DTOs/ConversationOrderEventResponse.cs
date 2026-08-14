using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed record ConversationOrderEventResponse
{
    public required string Id { get; init; }
    public string? ExternalOrderId { get; init; }
    public ConversationOrderEventStatus Status { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyList<ProductOrderSnapshotResponse> Products { get; init; } = [];

    public static ConversationOrderEventResponse FromEntity(ConversationOrderEvent entity) => new()
    {
        Id = entity.Id.ToString(),
        ExternalOrderId = entity.ExternalOrderId,
        Status = entity.Status,
        Amount = entity.Amount,
        CreatedAt = entity.CreatedAt,
        Products = entity.ProductOrderSnapshotItems.Select(product => new ProductOrderSnapshotResponse
        {
            ExternalProductId = product.ExternalProductId,
            ProductName = product.ProductName,
            Price = product.Price,
            Quantity = product.Quantity
        }).ToList()
    };
}

public sealed record ProductOrderSnapshotResponse
{
    public required string ExternalProductId { get; init; }
    public string? ProductName { get; init; }
    public decimal Price { get; init; }
    public int? Quantity { get; init; }
}
