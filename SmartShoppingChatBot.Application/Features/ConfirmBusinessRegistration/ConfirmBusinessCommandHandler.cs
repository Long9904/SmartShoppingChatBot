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
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConfirmBusinessRegistration;

public class ConfirmBusinessCommandHandler :
    IRequestHandler<ConfirmBusinessCommand, Result<BusinessRegistrationResponse>>
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly EmailTokenSettings _emailTokenSettings;
    private readonly TimeProvider _time;
    private readonly ILogger<ConfirmBusinessCommandHandler> _logger;

    public ConfirmBusinessCommandHandler(
        IBusinessRepository businessRepository,
        IUserRepository userRepository,
        ITokenRepository tokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        TimeProvider time,
        ILogger<ConfirmBusinessCommandHandler> logger,
        IOptions<EmailTokenSettings> emailTokenSettingsOptions
        )
    {
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _time = time;
        _logger = logger;
        _tokenService = tokenService;
        _emailTokenSettings = emailTokenSettingsOptions.Value;
    }

    public async Task<Result<BusinessRegistrationResponse>> Handle(
        ConfirmBusinessCommand request,
        CancellationToken cancellationToken)
    {

        var business = await _businessRepository.FindAsync(b => b.Id == request.BusinessId && b.BusinessStatus == BusinessEnums.PENDING_APPROVAL);

        if (business == null)
        {
            return Result<BusinessRegistrationResponse>.Failure(400, "Business not found");
        }

        var owner = await _userRepository.FindAsync(u => u.Business.Id == request.BusinessId && u.Business.Role == RoleEnums.BUSINESS_OWNER);

        if (owner == null) return Result<BusinessRegistrationResponse>.Failure(400, "Business owner not found");

        var updatedAt = _time.GetUtcNow();

        if (request.IsApproved == true)
        {
            business.BusinessStatus = BusinessEnums.ACTIVE;
            owner.UserStatus = UserStatus.PENDING_PROFILE_COMPLETION;
        }
        else
        {
            business.BusinessStatus = BusinessEnums.REJECTED;
            business.UpdatedAt = updatedAt;
            owner.UserStatus = UserStatus.REJECTED;
        }

        business.UpdatedAt = updatedAt;
        owner.UpdatedAt = updatedAt;


        var token = _tokenService.CreateEmailVerificationToken();
        var tokenHash = TokenHelper.BuildHashToken(token);

        var newToken = new Token
        {
            Id = ObjectId.GenerateNewId(),
            UserId = owner.Id,
            TokenValue = tokenHash,
            ExpiresAt = _time.GetUtcNow().AddDays(_emailTokenSettings.ExpireDays),
            CreatedAt = _time.GetUtcNow(),
            Type = TokenType.EMAIL_VERIFICATION
        };

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            await _businessRepository.UpdateAsync(business);
            await _userRepository.UpdateAsync(owner);
            await _tokenRepository.AddAsync(newToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync(cancellationToken);

            _logger.LogError(ex, "An error occurred while processing business registration for {BusinessId}.", request.BusinessId);
            return Result<BusinessRegistrationResponse>.Failure(500, "An error occurred while processing the business registration.");
        }

        var buildURL = $"{_emailTokenSettings.UrlBase}{Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(owner.Email)}";

        await _publishEndpoint.Publish(new BusinessRegistrationConfirmedEvent
        {
            BusinessId = business.Id.ToString(),
            BusinessName = business.BusinessName,
            OwnerEmail = owner.Email,
            OwnerName = owner.FullName,
            BusinessStatus = business.BusinessStatus,
            TokenVerification = business.BusinessStatus == BusinessEnums.ACTIVE ? buildURL : null
        }, cancellationToken);


        var response = new BusinessRegistrationResponse
        {
            Id = business.Id.ToString(),
            BusinessName = business.BusinessName,
            BusinessStatus = business.BusinessStatus
        };
        var message = request.IsApproved == true
            ? "Business registration approved successfully."
            : "Business registration rejected successfully.";

        return Result<BusinessRegistrationResponse>.Success(response, 200, message);
    }
}
