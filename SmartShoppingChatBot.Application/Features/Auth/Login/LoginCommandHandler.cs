using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordService _passwordService;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordService passwordService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordService = passwordService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindAsync(x => x.Email.ToLower() == request.Email.ToLower().Trim());

        if (user == null) return Result<LoginResponse>.Failure(401, "Invalid email or password");

        var isPasswordTrue = _passwordService.VerifyPassword(request.Password, user.PasswordHash);

        if (!isPasswordTrue) return Result<LoginResponse>.Failure(401, "Invalid email or password");

        if (user.UserStatus == UserStatus.PENDING_APPROVAL)
            return Result<LoginResponse>.Failure(403, "Your account is pending approval. Please wait for an admin to review your profile.");

        if (user.UserStatus == UserStatus.PENDING_PROFILE_COMPLETION)
            return Result<LoginResponse>.Failure(403, "Your account is pending profile completion. Please check your email for verification.");

        if (user.UserStatus != UserStatus.ACTIVE)
            return Result<LoginResponse>.Failure(403, "Your account is not active. Please contact support.");

        var payload = new AccessTokenPayload
        {
            UserId = user.Id.ToString(),
            Role = user.Business.Role,
            BusinessId = user.Business.Id.ToString(),
            BusinessName = user.Business.BusinessName ?? string.Empty,
        };

        var token = _tokenService.CreateAccessToken(payload);

        var res = new LoginResponse
        {
            AccessToken = token,
            IsEmailVerified = user.IsEmailVerified,
            IsProfileCompleted = user.IsProfileCompleted,
        };

        return Result<LoginResponse>.Success(res, 200, "Login successful");
    }
}
