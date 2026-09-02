using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRepository _userRepository;
    private readonly IBusinessRepository _businessRepository;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IUserRepository userRepository,
        IBusinessRepository businessRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _userRepository = userRepository;
        _businessRepository = businessRepository;
    }
    public async Task<Result<Business>> GetBusiness()
    {
        var businessId = _httpContextAccessor.HttpContext?.User?.FindFirst("business")?.Value;

        if (businessId == null)
        {
            return Result<Business>.Failure(401, "Token is invalid", messageCode: AuthMessageCode.InvalidAuthentication);
        }

        var isValidId = ObjectId.TryParse(businessId, out var objectId);
        if (!isValidId)
        {
            return Result<Business>.Failure(401, "Token is invalid", messageCode: AuthMessageCode.InvalidAuthentication);
        }
        var business = await _businessRepository.FindAsync(b => b.Id == objectId);
        if (business == null)
        {
            return Result<Business>.Failure(404, "Business not found", messageCode: BusinessMessageCode.NotFound);
        }

        return business.BusinessStatus switch
        {
            BusinessEnums.ACTIVE => Result<Business>.Success(business, 200, "Get business success", BusinessMessageCode.Sucess),

            BusinessEnums.PENDING_APPROVAL => Result<Business>.Failure(400, "Business is waiting to approve", messageCode: BusinessMessageCode.WattingApprove),

            BusinessEnums.REJECTED => Result<Business>.Failure(400, "Business is rejected.", messageCode: BusinessMessageCode.IsRejected),

            BusinessEnums.DELETED => Result<Business>.Failure(404, "Business not found.", null, BusinessMessageCode.NotFound),

            _ => Result<Business>.Failure(401, "Token is invalid.", messageCode: AuthMessageCode.InvalidAuthentication)
        };
    }

    public string? GetBusinessId()
    {
        var businessId = _httpContextAccessor.HttpContext?.User?.FindFirst("business")?.Value;

        if (businessId == null)
        {
            throw new AuthenticationException("Token is invalid");
        }

        var isValidId = ObjectId.TryParse(businessId, out var objectId);
        if (!isValidId)
        {
            throw new AuthenticationException("Token is invalid");
        }

        return businessId;
    }

    public string? GetIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext == null)
            return null;

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor
                .Split(',')
                .Select(x => x.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    public async Task<Result<User>> GetUser()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Result<User>.Failure(401, "Token is invalid.", messageCode: AuthMessageCode.InvalidAuthentication);
        }

        var isValidId = ObjectId.TryParse(userId, out var objectId);
        if (!isValidId)
        {
            return Result<User>.Failure(401, "Token is invalid.", messageCode: AuthMessageCode.InvalidAuthentication);
        }

        var user = await _userRepository.FindAsync(u => u.Id == objectId);
        if (user == null) return Result<User>.Failure(404, "User not found.", messageCode: UserMessageCode.NotFound);

        return user.UserStatus switch
        {
            UserStatus.ACTIVE => Result<User>.Success(user, 200, "User retrieved successfully.", messageCode: UserMessageCode.Success),

            UserStatus.PENDING_APPROVAL => Result<User>.Failure(400, "User is pending approval.", messageCode: UserMessageCode.WattingApprove),

            UserStatus.PENDING_PROFILE_COMPLETION => Result<User>.Failure(400, "User is pending profile completion.", messageCode: UserMessageCode.ProfilePending),

            UserStatus.REJECTED => Result<User>.Failure(400, "User is rejected.", messageCode: UserMessageCode.IsRejected),

            UserStatus.DELETED => Result<User>.Failure(404, "User not found.", messageCode: UserMessageCode.NotFound),

            _ => Result<User>.Failure(401, "Token is invalid.", messageCode: AuthMessageCode.InvalidAuthentication)
        };
    }

    public async Task<string?> GetUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new AuthenticationException("Token is invalid");

        var isValidId = ObjectId.TryParse(userId, out var objectId);
        if (!isValidId)
        {
            throw new AuthenticationException("Token is invalid.");
        }

        var user = await _userRepository.FindAsync(u => u.Id == objectId);

        return user.UserStatus switch
        {
            UserStatus.ACTIVE => userId,
            UserStatus.PENDING_APPROVAL => throw new AuthenticationException("User is pending approval."),
            UserStatus.PENDING_PROFILE_COMPLETION => throw new AuthenticationException("User is pending profile completion."),
            UserStatus.REJECTED => throw new AuthenticationException("User is rejected."),
            UserStatus.DELETED => throw new AuthenticationException("User is deleted."),
            _ => throw new AuthenticationException("Token is invalid.")
        };
    }
}
