using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ConversationOrderEventRequest
{
    public string? ExternalOrderId { get; init; }

    public ConversationOrderEventStatus Status { get; init; }

    public decimal Amount { get; init; }

    public List<ProductOrderSnapshotRequest> Products { get; init; } = [];
}

public sealed class ProductOrderSnapshotRequest
{
    public string ExternalProductId { get; init; } = string.Empty;

    public string? ProductName { get; init; }

    public decimal Price { get; init; }

    public int? Quantity { get; init; }
}
