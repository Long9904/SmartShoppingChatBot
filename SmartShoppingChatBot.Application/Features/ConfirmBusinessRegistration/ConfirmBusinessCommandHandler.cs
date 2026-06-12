using MassTransit;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConfirmBusinessRegistration;

public class ConfirmBusinessCommandHandler :
    IRequestHandler<ConfirmBusinessCommand, Result<BusinessRegistrationResponse>>
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _time;

    public ConfirmBusinessCommandHandler(
        IBusinessRepository businessRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IUserRepository userRepository,
        TimeProvider time)
    {
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _userRepository = userRepository;
        _time = time;
    }

    public async Task<Result<BusinessRegistrationResponse>> Handle(
        ConfirmBusinessCommand request,
        CancellationToken cancellationToken)
    {
        var business = await _businessRepository.FindAsync(b => b.Id == request.BusinessId);
        if (business == null)
        {
            return Result<BusinessRegistrationResponse>.Failure(400, "Business not found");
        }

        if (business.BusinessStatus != BusinessEnums.PENDING_APPROVAL)
        {
            return Result<BusinessRegistrationResponse>.Failure(400, "Business cannot be processed");
        }

        var owner = await _userRepository.FindAsync(u => u.Businesses.Any(
            b => b.Id == business.Id && b.Role == RoleEnums.BUSINESS_OWNER));

        if (owner == null) return Result<BusinessRegistrationResponse>.Failure(400, "Business owner not found");

        if (request.IsApproved)
        {
            business.BusinessStatus = BusinessEnums.APPROVED;
            business.UpdatedAt = _time.GetUtcNow();

            owner.UserStatus = UserStatus.PENDING_PROFILE_COMPLETION;
            owner.UpdatedAt = _time.GetUtcNow();
        }
        else
        {
            business.BusinessStatus = BusinessEnums.REJECTED;
            business.UpdatedAt = _time.GetUtcNow();

            owner.UserStatus = UserStatus.REJECTED;
            owner.UpdatedAt = _time.GetUtcNow();
        }


        try
        {
            await _unitOfWork.BeginTransactionAsync();
            await _businessRepository.UpdateAsync(business);
            await _userRepository.UpdateAsync(owner);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        } catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync();
            return Result<BusinessRegistrationResponse>.Failure(500, $"An error occurred while processing the business registration: {ex.Message}");
        }

        await _publishEndpoint.Publish(new BusinessRegistrationConfirmedEvent
        {
            BusinessId = business.Id.ToString(),
            BusinessName = business.BusinessName,
            OwnerEmail = owner.Email,
            OwnerName = owner.FullName,
            BusinessStatus = business.BusinessStatus
        });



        var response = new BusinessRegistrationResponse
        {
            Id = business.Id.ToString(),
            BusinessName = business.BusinessName,
            BusinessStatus = business.BusinessStatus
        };
        return Result<BusinessRegistrationResponse>.Success(response, 200, "Verify business registration successful");
    }
}
