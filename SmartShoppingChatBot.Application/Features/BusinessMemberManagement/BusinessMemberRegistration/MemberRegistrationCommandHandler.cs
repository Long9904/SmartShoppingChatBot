using AutoMapper;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Commons.Utils;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.BusinessMemberRegistration
{
    public class MemberRegistrationCommandHandler :
        IRequestHandler<MemberRegistrationCommand, Result<BusinessMemberRegistrationResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<MemberRegistrationCommandHandler> _logger;
        private readonly EmailTokenSettings _emailTokenSettings;
        private readonly TimeProvider _time;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ITokenService _tokenService;
        private readonly ITokenRepository _tokenRepository;

        public MemberRegistrationCommandHandler(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<MemberRegistrationCommandHandler> logger,
            TimeProvider time,
            ICurrentUserService currentUserService,
            IPublishEndpoint publishEndpoint,
            ITokenService tokenService,
            ITokenRepository tokenRepository,
            IOptions<EmailTokenSettings> emailTokenSettingsOptions)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _time = time;
            _currentUserService = currentUserService;
            _publishEndpoint = publishEndpoint;
            _tokenService = tokenService;
            _tokenRepository = tokenRepository;
            _emailTokenSettings = emailTokenSettingsOptions.Value;
        }

        public async Task<Result<BusinessMemberRegistrationResponse>> Handle(MemberRegistrationCommand request, CancellationToken cancellationToken)
        {
            var isUser = await _currentUserService.GetUser();

            if (!isUser.IsSuccess)
            {
                return Result<BusinessMemberRegistrationResponse>.Failure(isUser.StatusCode, isUser.Message);
            }

            var businessOwner = isUser.Data;

            var emailExists = await _userRepository.FindAsync(u => u.Email == request.Email && u.Business.Id == businessOwner.Business.Id);

            if (emailExists != null)
            {
                return Result<BusinessMemberRegistrationResponse>.Failure(409, "Email already exists for this business.");
            }

            var dateNow = _time.GetUtcNow();

            var employee = new User
            {
                Id = ObjectId.GenerateNewId(),
                FullName = request.FullName,
                Email = request.Email,
                IsEmailVerified = false,
                IsProfileCompleted = false,
                PasswordHash = string.Empty,
                CreatedAt = dateNow,
                UpdatedAt = dateNow,
                UserStatus = UserStatus.PENDING_PROFILE_COMPLETION,
                CreatedBy = new UserEmbedded
                {
                    Id = businessOwner.Id,
                    Name = businessOwner.FullName,
                },

                Business = new BusinessEmbedded
                {
                    Id = businessOwner.Business.Id,
                    BusinessName = businessOwner.Business.BusinessName,
                    Role = RoleEnums.CATALOG_TEAM,
                    JoinedAt = dateNow,
                }
            };

            var token = _tokenService.CreateEmailVerificationToken();
            var tokenHash = TokenHelper.BuildHashToken(token);

            var newToken = new Token
            {
                Id = ObjectId.GenerateNewId(),
                UserId = employee.Id,
                TokenValue = tokenHash,
                ExpiresAt = _time.GetUtcNow().AddDays(_emailTokenSettings.ExpireDays),
                CreatedAt = dateNow,
                Type = TokenType.EMAIL_VERIFICATION
            };

            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _userRepository.AddAsync(employee);
                await _tokenRepository.AddAsync(newToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while registering employee.");
                await _unitOfWork.RollBackAsync(cancellationToken);
                return Result<BusinessMemberRegistrationResponse>.Failure(500, "An error occurred while processing your request.");
            }

            var buildURL = $"{_emailTokenSettings.UrlBase}{Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(employee.Email)}";


            await _publishEndpoint.Publish(new EmployeeRegistrationConfirmedEvent
            {
                BusinessName = businessOwner.Business.BusinessName,
                EmployeeName = employee.FullName,
                EmployeeEmail = employee.Email,
                TokenVerification = buildURL,
            }, cancellationToken);

            return Result<BusinessMemberRegistrationResponse>.Success(new BusinessMemberRegistrationResponse
            {
                Id = employee.Id.ToString(),
                FullName = employee.FullName,
                Email = employee.Email,
            }, message: "Employee registered successfully. Please check your email to verify your account.");
        }
    }
}
