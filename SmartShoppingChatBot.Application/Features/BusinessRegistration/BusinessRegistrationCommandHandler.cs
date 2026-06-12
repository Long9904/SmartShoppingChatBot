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
        var validationResult = await ValidateBusinessRegistrationAsync(request, _businessRepository, _userRepository);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        var businessId = ObjectId.GenerateNewId();
        var userId = ObjectId.GenerateNewId();
        var dateNow = _time.GetLocalNow();

        var business = new Business
        {
            Id = businessId,
            BusinessName = request.BusinessName,
            HotLine = request.HotLine,
            WebsiteUrl = request.WebsiteUrl,
            AddressLine = request.AddressLine,
            BusinessStatus = BusinessEnums.PENDING_APPROVAL,
            CreatedAt = dateNow,
            UpdatedAt = dateNow,
            CreatedBy = new UserEmbedded
            {
                Id = userId,
                Name = request.BusinessOwnerName,
            }
        };

        var ownerUSer = new User
        {
            Id = userId,
            Email = request.BusinessOwnerEmail,
            FullName = request.BusinessOwnerName,
            IsEmailVerified = false,
            IsProfileCompleted = false,
            PasswordHash = string.Empty,
            CreatedAt = dateNow,
            UpdatedAt = dateNow,
            UserStatus = UserStatus.PENDING_APPROVAL,
            CreatedBy = new UserEmbedded
            {
                Id = userId,
                Name = request.BusinessOwnerName,
            },
            Businesses = new List<BusinessEmbedded>
            {
                new BusinessEmbedded
                {
                    Id = business.Id,
                    BusinessName = business.BusinessName,
                    Role = RoleEnums.BUSINESS_OWNER,
                    JoinedAt = dateNow,
                }
            }
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _businessRepository.AddAsync(business);
            await _userRepository.AddAsync(ownerUSer);
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
        _logger.LogInformation($"Business with name {business.BusinessName} registered successfully.");
        return Result<BusinessRegistrationResponse>.Success(response, 201, "Business registered successfully.");
    }


    public async Task<Result<BusinessRegistrationResponse>> ValidateBusinessRegistrationAsync(
        BusinessRegistrationCommand request,
        IBusinessRepository businessRepository,
        IUserRepository userRepository)
    {
        var existingUser = await userRepository.FindAsync(
        u => u.Email == request.BusinessOwnerEmail);

        if (existingUser != null)
        {
            return existingUser.UserStatus switch
            {
                UserStatus.ACTIVE => Result<BusinessRegistrationResponse>.Failure(409, "This email is already registered."),

                UserStatus.PENDING_APPROVAL =>
                    Result<BusinessRegistrationResponse>.Failure(400, "This email is already waiting for admin approval."),

                UserStatus.PENDING_PROFILE_COMPLETION =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email has been approved and is waiting for profile completion."),

                UserStatus.REJECTED =>
                    Result<BusinessRegistrationResponse>.Failure(400, "This email was rejected. Please contact support."),

                _ =>
                    Result<BusinessRegistrationResponse>.Failure(409, "This email is already used.")
            };
        }

        var existingHotline = await businessRepository.GetByHotlineAsync(request.HotLine);

        if (existingHotline != null)
        {
            return Result<BusinessRegistrationResponse>.Failure(
                409,
                "A business with the same hotline already exists.");
        }

        return Result<BusinessRegistrationResponse>.Success(null!, 200);
    }
}
