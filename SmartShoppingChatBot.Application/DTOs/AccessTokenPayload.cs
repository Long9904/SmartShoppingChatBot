using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class AccessTokenPayload
{
    public string UserId { get; init; } = default!;
    public RoleEnums Role { get; init; } = default!;
    public string BusinessId { get; init; } = default!;

    public string BusinessName { get; init; } = default!;
}
