using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface ITokenService
{
    string CreateAccessToken(AccessTokenPayload payload);
    string CreateTempToken(string userId);
}
