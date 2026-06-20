using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UpdateBusiness;

public class UpdateBusinessCommandHandler : IRequestHandler<UpdateBusinessCommand, Result<BusinessResponse>>
{
    private readonly IBusinessRepository _businessRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateBusinessCommandHandler> _logger;
    private readonly TimeProvider _time;

    public UpdateBusinessCommandHandler(
        IBusinessRepository businessRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateBusinessCommandHandler> logger,
        TimeProvider time)
    {
        _businessRepository = businessRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<BusinessResponse>> Handle(UpdateBusinessCommand request, CancellationToken cancellationToken)
    {
        var currentBusinessResult = await _currentUserService.GetBusiness();
        if (!currentBusinessResult.IsSuccess)
        {
            return Result<BusinessResponse>.Failure(
                currentBusinessResult.StatusCode,
                currentBusinessResult.Message,
                currentBusinessResult.Errors);
        }

        var business = currentBusinessResult.Data!;

        var existingHotline = await _businessRepository.GetByHotlineAsync(request.HotLine.Trim());
        if (existingHotline != null && existingHotline.Id != business.Id)
        {
            return Result<BusinessResponse>.Failure(409, "A business with the same hotline already exists.");
        }

        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<BusinessResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        var updatedAt = _time.GetUtcNow();
        var businessName = request.BusinessName.Trim();
        business.BusinessName = businessName;
        business.HotLine = request.HotLine.Trim();
        business.WebsiteUrl = request.WebsiteUrl.Trim();
        business.AddressLine = request.AddressLine.Trim();
        business.UpdatedAt = updatedAt;
        business.UpdatedBy = new UserEmbedded
        {
            Id = currentUserResult.Data!.Id,
            Name = currentUserResult.Data.FullName
        };

        var businessUsers = await _userRepository.FindAllAsync(
            user => user.Business.Id == business.Id && user.UserStatus != UserStatus.DELETED);

        foreach (var user in businessUsers)
        {
            user.Business.BusinessName = businessName;
            user.UpdatedAt = updatedAt;
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _businessRepository.UpdateAsync(business);
            foreach (var user in businessUsers)
            {
                await _userRepository.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync(cancellationToken);
            _logger.LogError(ex, "An error occurred while updating business {BusinessId}.", business.Id);
            return Result<BusinessResponse>.Failure(500, "An error occurred while updating the business.");
        }

        var response = _mapper.Map<BusinessResponse>(business);
        return Result<BusinessResponse>.Success(response, 200, "Business updated successfully.");
    }
}
