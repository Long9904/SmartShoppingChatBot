using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
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
            return Result<Business>.Failure(401, "Token is invalid.");
        }

        var isValidId = ObjectId.TryParse(businessId, out var objectId);
        if (!isValidId)
        {
            return Result<Business>.Failure(401, "Token is invalid.");
        }
        var business = await _businessRepository.FindAsync(b => b.Id == objectId);
        if (business == null)
        {
            return Result<Business>.Failure(404, "Business not found.");
        }

        return business.BusinessStatus switch 
        {
            BusinessEnums.ACTIVE => Result<Business>.Success(business, 200, "Business retrieved successfully."),
            BusinessEnums.PENDING_APPROVAL => Result<Business>.Failure(403, "Business is pending approval."),
            BusinessEnums.REJECTED => Result<Business>.Failure(403, "Business is rejected."),
            BusinessEnums.DELETED => Result<Business>.Failure(403, "Business is deleted."),
            _ =>  Result<Business>.Failure(401, "Token is invalid.")
        };
    }

    public async Task<Result<User>> GetUser()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Result<User>.Failure(401, "Token is invalid.");
        }

        var isValidId = ObjectId.TryParse(userId, out var objectId);
        if (!isValidId)
        {
            return Result<User>.Failure(401, "Token is invalid.");
        }

        var user = await _userRepository.FindAsync(u => u.Id == objectId);
        if (user == null) return Result<User>.Failure(404, "User not found.");

        return user.UserStatus switch 
        {
            UserStatus.ACTIVE => Result<User>.Success(user, 200, "User retrieved successfully."),
            UserStatus.PENDING_APPROVAL => Result<User>.Failure(403, "User is pending approval."),
            UserStatus.PENDING_PROFILE_COMPLETION => Result<User>.Failure(403, "User is pending profile completion."),
            UserStatus.REJECTED => Result<User>.Failure(403, "User is rejected."),
            UserStatus.DELETED => Result<User>.Failure(403, "User is deleted."),
            _ => Result<User>.Failure(401, "Token is invalid.")
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
