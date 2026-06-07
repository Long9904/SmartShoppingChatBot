
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface;

public interface ITokenService
{
    string CreateAccessToken(AccessTokenPayload payload, DateTime expUtc);
}
