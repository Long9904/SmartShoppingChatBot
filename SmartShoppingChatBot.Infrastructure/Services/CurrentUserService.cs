using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    public string? GetBusinessId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst("business")?.Value ?? throw new AuthenticationException("Token is invalid 0");
    }

    public string? GetUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new AuthenticationException("Token is invalid 1.");
    }
}
