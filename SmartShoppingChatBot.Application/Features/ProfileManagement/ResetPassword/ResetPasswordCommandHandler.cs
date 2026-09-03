using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Commons.Utils;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ResetPassword
{
    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<string>>
    {
        private readonly ITokenService _tokenService;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordService _passwordService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActivityLogService _activityLogService;

        public ResetPasswordCommandHandler(ITokenService tokenService, ITokenRepository tokenRepository,
            IUserRepository userRepository, IPasswordService passwordService, IUnitOfWork unitOfWork,
            IActivityLogService activityLogService)
        {
            _tokenService = tokenService;
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
            _passwordService = passwordService;
            _unitOfWork = unitOfWork;
            _activityLogService = activityLogService;
        }
        public async Task<Result<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var token = TokenHelper.BuildHashToken(request.Token);
            var tokenEntity = await _tokenRepository.FindAsync(x => x.TokenValue == token && x.Type == Domain.Enums.TokenType.PASSWORD_RESET);
            if(tokenEntity == null || tokenEntity.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return Result<string>.Failure(400, "Invalid or expired token.");
            }
          
            var user = await _userRepository.FindAsync(x => x.Id == tokenEntity.UserId);
            if(user == null)
            {
                return Result<string>.Failure(404, "User not found.");
            }
            if(request.NewPassword != request.ConfirmPassword)
            {
                return Result<string>.Failure(400, "Passwords do not match.");
            }

            var hashedPassword = _passwordService.HashPassword(request.NewPassword);
            user.PasswordHash = hashedPassword;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            user.UpdatedBy = new()
            {
                Id = user.Id,
                Name = user.FullName,
            };
            await _userRepository.UpdateAsync(user);
            tokenEntity.TokenValue = null!;
            await _tokenRepository.UpdateAsync(tokenEntity);
            await _unitOfWork.SaveChangesAsync();
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                Action = ActionLogEnums.PasswordReset,
                ActorId = user.Id.ToString(),
                TargetType = "User",
                TargetId = user.Id.ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = $"User {user.FullName} reset password successfully.",
            });
            return Result<string>.Success(null, 200, "Password reset successfully.");

        }
    }
}
