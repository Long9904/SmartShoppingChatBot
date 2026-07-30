using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class CustomerListResponse
{
    public string Id { get; set; } = string.Empty;

    public string CustomerExternalId { get; set; } = string.Empty;

    public string? Name { get; set; }

    public CustomerStatus Status { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
