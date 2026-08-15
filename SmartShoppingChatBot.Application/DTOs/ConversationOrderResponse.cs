using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed record ConversationOrderResponse
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public required string ExternalOrderId { get; init; }
    public ConversationOrderEventStatus Status { get; init; }
    public decimal Amount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<ProductOrderSnapshotResponse> Products { get; init; } = [];

    public static ConversationOrderResponse FromEntity(ConversationOrder entity) => new()
    {
        Id = entity.Id.ToString(),
        ConversationId = entity.ConversationId.ToString(),
        ExternalOrderId = entity.ExternalOrderId,
        Status = entity.Status,
        Amount = entity.Amount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Products = entity.ProductOrderSnapshotItems.Select(product => new ProductOrderSnapshotResponse
        {
            ExternalProductId = product.ExternalProductId,
            ProductName = product.ProductName,
            Price = product.Price,
            Quantity = product.Quantity
        }).ToList()
    };
}
