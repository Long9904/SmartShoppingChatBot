namespace SmartShoppingChatBot.Application.DTOs;

public class AccessTokenPayload
{
    public string UserId { get; init; } = default!;
    public string Role { get; init; } = default!;
    public string BusinessId { get; init; } = default!;
}
