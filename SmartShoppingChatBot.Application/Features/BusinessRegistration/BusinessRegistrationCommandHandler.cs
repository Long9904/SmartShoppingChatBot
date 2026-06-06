using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessRegistration;

public class BusinessRegistrationCommandHandler : 
    IRequestHandler<BusinessRegistrationCommand, Result<BusinessRegistrationResponse>>

{
    private readonly IBusinessRepository _businessRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BusinessRegistrationCommandHandler(
        IBusinessRepository businessRepository, 
        IUnitOfWork unitOfWork)
    {
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BusinessRegistrationResponse>> Handle(
        BusinessRegistrationCommand request, 
        CancellationToken cancellationToken)
    {
        var existingBusinessEmail = await _businessRepository.GetByEmailAsync(request.Email);
        if (existingBusinessEmail != null)
        {
            return Result<BusinessRegistrationResponse>.Failure(409 , "A business with the same email already exists.");
        }

        var existingBusinessHotLine = await _businessRepository.GetByHotlineAsync(request.HotLine);
        if (existingBusinessHotLine != null)
        {
            return Result<BusinessRegistrationResponse>.Failure( 409 , "A business with the same hotline already exists.");
        }

        var business = new Business
        {
            BusinessName = request.BusinessName,
            Email = request.Email,
            HotLine = request.HotLine,
            WebsiteUrl = request.WebsiteUrl,
            AddressLine = request.AddressLine,
            City = request.City,
            BrandAssetsUrl = request.BrandAssetsUrl,
            BusinessStatus = Domain.Enums.BusinessEnums.PENDING,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = new UserEmbedded
            {
                Name = "Business Owner"
            }
        };

        await _businessRepository.AddAsync(business);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new BusinessRegistrationResponse
        {
            Id = business.Id,
            BusinessName = business.BusinessName,
            Email = business.Email,
            HotLine = business.HotLine,
            WebsiteUrl = business.WebsiteUrl,
            AddressLine = business.AddressLine,
            City = business.City,
            BrandAssetsUrl = business.BrandAssetsUrl,
            BusinessStatus = business.BusinessStatus
        };

        return Result<BusinessRegistrationResponse>.Success(response, 201, "Business registered successfully.");
    }
}
