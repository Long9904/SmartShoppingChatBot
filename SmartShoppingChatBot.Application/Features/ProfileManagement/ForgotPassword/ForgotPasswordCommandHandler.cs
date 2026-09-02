using Google.Apis.Auth.OAuth2;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Options;
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

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ForgotPassword
{
    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<string>>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly ITokenService _tokenAccess;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PasswordResetTokenSettings _passwordResetTokenSettings;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IActivityLogService _activityLogService;

        public ForgotPasswordCommandHandler(IUserRepository userRepository, ITokenRepository tokenRepository,
            ITokenService tokenAccess, IEmailService emailService, IUnitOfWork unitOfWork, IOptions<PasswordResetTokenSettings> passwordResetTokenSettingsOptions, IEmailTemplateService emailTemplateService,
            IActivityLogService activityLogService)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _tokenAccess = tokenAccess;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _passwordResetTokenSettings = passwordResetTokenSettingsOptions.Value;
            _emailTemplateService = emailTemplateService;
            _activityLogService = activityLogService;
        }

        public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.FindAsync(x => x.Email == request.Email.ToLower().Trim() && x.UserStatus == Domain.Enums.UserStatus.ACTIVE);

            if (user == null)
            {
                return Result<string>.Success("If the email exists, a reset password link has been sent.", 200);
            }
            var rawToken = _tokenAccess.CreateEmailVerificationToken();
            var tokenHash = TokenHelper.BuildHashToken(rawToken);
            var token = new Domain.Entities.Token
            {
                Id = ObjectId.GenerateNewId(),
                UserId = user.Id,
                TokenValue = tokenHash,
                Type = Domain.Enums.TokenType.PASSWORD_RESET,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_passwordResetTokenSettings.ExpireMinutes)
            };

            await _tokenRepository.AddAsync(token);
            await _unitOfWork.SaveChangesAsync();
            var buildURL = $"{_passwordResetTokenSettings.UrlBase}{Uri.EscapeDataString(rawToken)}&email={Uri.EscapeDataString(user.Email)}";
            var body = await _emailTemplateService.RenderEmailTemplateAsync(
                        "ResetPassword",
                        new ResetPasswordEmailModel
                        {
                            FullName = user.FullName,
                            Email = user.Email,
                            ResetPasswordUrl = buildURL,
                            ExpireMinutes = _passwordResetTokenSettings.ExpireMinutes
                        });
            await _emailService.SendEmailAsync(user.Email, "Password Reset", body);
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                Action = ActionLogEnums.PasswordReset,
                ActorId = user.Id.ToString(),
                TargetType = "User",
                TargetId = user.Id.ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = $"User {user.FullName} requested a password reset successfully.",
            });

            return Result<string>.Success("Password reset instructions sent to your email.");
        }
    }
}
