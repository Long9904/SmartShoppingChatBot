using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Commons.Utils;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.VerifyAccount;

public class VerifyAccountCommandHandler : IRequestHandler<VerifyAccountCommand, Result<bool>>
{

    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IBusinessRepository _businessRepository;
    private readonly TimeProvider _time;
    private readonly ILogger<VerifyAccountCommandHandler> _logger;

    public VerifyAccountCommandHandler(
        IUnitOfWork unitOfWork,
        ITokenRepository tokenRepository,
        IUserRepository userRepository,
        IPasswordService passwordService,
        IBusinessRepository businessRepository,
        TimeProvider timeProvider,
        ILogger<VerifyAccountCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _passwordService = passwordService;
        _logger = logger;
        _time = timeProvider;
        _businessRepository = businessRepository;
    }

    public async Task<Result<bool>> Handle(VerifyAccountCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Sha256(request.Token);
        Console.WriteLine(tokenHash);
        // Check if the token exists and is valid
        var existingToken = await _tokenRepository.FindAsync(t => t.TokenValue == tokenHash && t.Type == TokenType.EMAIL_VERIFICATION);

        if (existingToken == null)
        {
            return Result<bool>.Failure(400, "Invalid token.");
        }

        if (existingToken.ExpiresAt < _time.GetUtcNow())
        {
            return Result<bool>.Failure(400, "Token has expired.");
        }

        // Get the user associated with the token
        var user = await _userRepository.FindAsync(u => u.Id == existingToken.UserId);
        if (user == null)
        {
            return Result<bool>.Failure(400, "User not found.");
        }

        if (user.IsEmailVerified || user.EmailVerifiedAt != null)
        {
            return Result<bool>.Failure(400, "Email is already verified.");
        }

        var business = await _businessRepository.FindAsync(b => b.Id == user.Business.Id);
        if (business == null)
        {
            return Result<bool>.Failure(400, "Associated business not found.");
        }
        business.BusinessStatus = BusinessEnums.ACTIVE;

        // Update the user's email verification status
        var timeNow = _time.GetUtcNow();
        user.IsEmailVerified = true;
        user.IsProfileCompleted = true;
        user.EmailVerifiedAt = timeNow;
        user.UpdatedAt = timeNow;
        user.PasswordHash = _passwordService.HashPassword(request.Password).Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.Gender = request.Gender!.Value;
        user.UserStatus = UserStatus.ACTIVE;

        existingToken.TokenValue = null!;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            await _userRepository.UpdateAsync(user);
            await _tokenRepository.UpdateAsync(existingToken);
            await _businessRepository.UpdateAsync(business);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<bool>.Success(true, 200, "Account verified successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying account for user {UserId}", user.Id);
            await _unitOfWork.RollBackAsync(cancellationToken);
            return Result<bool>.Failure(500, "An error occurred while verifying the account.");

        }
    }
}
