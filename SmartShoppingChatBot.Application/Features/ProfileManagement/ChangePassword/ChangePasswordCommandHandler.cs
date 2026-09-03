using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ChangePassword
{
    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<string>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordService _passwordService;
        private readonly ILogger<ChangePasswordCommandHandler> _logger;
        private readonly IActivityLogService _activityLogService;


        public ChangePasswordCommandHandler(ICurrentUserService currentUserService, IUserRepository userRepository,
            IUnitOfWork unitOfWork, ILogger<ChangePasswordCommandHandler> logger, IPasswordService passwordService,
            IActivityLogService activityLogService)
        {
            _currentUserService = currentUserService;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _passwordService = passwordService;
            _activityLogService = activityLogService;
        }


        public async Task<Result<string>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var userLogin = await _currentUserService.GetUser();

            if (userLogin == null)
            {
                return Result<string>.Failure(404, "User not found");
            }
            var user = userLogin.Data;
            if (user == null)
            {
                return Result<string>.Failure(404, "User not found");
            }
            if (!_passwordService.VerifyPassword(request.currentPassword, user.PasswordHash))
            {
                return Result<string>.Failure(400, "Current password is incorrect");
            }
            if (request.newPassword != request.confirmPassword)
            {
                return Result<string>.Failure(400, "New password and confirm password do not match");
            }
            var newPasswordHash = _passwordService.HashPassword(request.newPassword);
            user.PasswordHash = newPasswordHash;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = new()
            {
                Id = user.Id,
                Name = user.FullName
            };
            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                Action = ActionLogEnums.PasswordChange,
                ActorId = user.Id.ToString(),
                TargetType = "User",
                TargetId = user.Id.ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = $"User {user.FullName} changed password successfully.",
            });
            _logger.LogInformation($"User {user.FullName} changed password successfully.");
            return Result<string>.Success("Password changed successfully", 200);
        }
    }
}
