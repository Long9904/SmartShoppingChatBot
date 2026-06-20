using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessRegistration;

public class BusinessRegistrationCommandHandler :
    IRequestHandler<BusinessRegistrationCommand, Result<BusinessRegistrationResponse>>

{
    private readonly IBusinessRepository _businessRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<BusinessRegistrationCommandHandler> _logger;
    private readonly TimeProvider _time;

    public BusinessRegistrationCommandHandler(
        IBusinessRepository businessRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<BusinessRegistrationCommandHandler> logger,
        TimeProvider time)
    {
        _businessRepository = businessRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<BusinessRegistrationResponse>> Handle(
        BusinessRegistrationCommand request,
        CancellationToken cancellationToken)
    {
        var validationResult = await ValidateBusinessRegistrationAsync(request);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        var businessId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var dateNow = _time.GetUtcNow();
        var ownerName = request.BusinessOwnerName.Trim();
        var ownerEmail = request.BusinessOwnerEmail.Trim();
        var businessName = request.BusinessName.Trim();

        var business = new Business
        {
            Id = businessId,
            BusinessName = businessName,
            HotLine = request.HotLine.Trim(),
            WebsiteUrl = request.WebsiteUrl.Trim(),
            AddressLine = request.AddressLine.Trim(),
            BusinessStatus = BusinessEnums.PENDING_APPROVAL,
            CreatedAt = dateNow,
            UpdatedAt = dateNow,
            CreatedBy = new UserEmbedded
            {
                Id = userId,
                Name = ownerName,
            }
        };

        var ownerUser = new User
        {
            Id = userId,
            Email = ownerEmail,
            FullName = ownerName,
            IsEmailVerified = false,
            IsProfileCompleted = false,
            PasswordHash = string.Empty,
            CreatedAt = dateNow,
            UpdatedAt = dateNow,
            UserStatus = UserStatus.PENDING_APPROVAL,
            CreatedBy = new UserEmbedded
            {
                Id = userId,
                Name = ownerName,
            },
            Business = new BusinessEmbedded
            {
                Id = business.Id,
                BusinessName = business.BusinessName,
                Role = RoleEnums.BUSINESS_OWNER,
                JoinedAt = dateNow,

            }
        };

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _businessRepository.AddAsync(business);
            await _userRepository.AddAsync(ownerUser);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync(cancellationToken);
            _logger.LogError(ex, "An error occurred while registering the business.");
            return Result<BusinessRegistrationResponse>.Failure(500, "An error occurred while registering the business.");
        }

        var response = _mapper.Map<BusinessRegistrationResponse>(business);
        _logger.LogInformation("Business with name {BusinessName} registered successfully.", business.BusinessName);
        return Result<BusinessRegistrationResponse>.Success(response, 201, "Business registered successfully.");
    }


    private async Task<Result<BusinessRegistrationResponse>> ValidateBusinessRegistrationAsync(
        BusinessRegistrationCommand request)
    {
        var normalizedOwnerEmail = request.BusinessOwnerEmail.Trim().ToLower();
        var existingUser = await _userRepository.FindAsync(
            u => u.Email.ToLower() == normalizedOwnerEmail);

        if (existingUser != null)
        {
            return existingUser.UserStatus switch
            {
                UserStatus.ACTIVE => Result<BusinessRegistrationResponse>.Failure(409, "This email is already registered."),

                UserStatus.PENDING_APPROVAL =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email is already waiting for admin approval."),

                UserStatus.PENDING_PROFILE_COMPLETION =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email has been approved and is waiting for profile completion."),

                UserStatus.REJECTED =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email was rejected. Please contact support."),

                _ =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email is already used.")
            };
        }

        var existingHotline = await _businessRepository.GetByHotlineAsync(request.HotLine.Trim());

        if (existingHotline != null)
        {
            return Result<BusinessRegistrationResponse>.Failure(
                409,
                "A business with the same hotline already exists.");
        }

        return Result<BusinessRegistrationResponse>.Success(null!, 200);
    }
}
