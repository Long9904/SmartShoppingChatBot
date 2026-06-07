using AutoMapper;
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
    private readonly IMapper _mapper;

    public BusinessRegistrationCommandHandler(
        IBusinessRepository businessRepository, 
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
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
            CreatedBy = new UserEmbedded
            {
                Name = "Business Owner"
            }
        };

        await _businessRepository.AddAsync(business);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = _mapper.Map<BusinessRegistrationResponse>(business);

        return Result<BusinessRegistrationResponse>.Success(response, 201, "Business registered successfully.");
    }
}
