using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.ResetBusinessConfig;

public class ResetBusinessConfigCommandHandler
    : IRequestHandler<ResetBusinessConfigCommand, Result<BusinessConfigResponse>>
{
    private readonly IBusinessRepository _businessRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<ResetBusinessConfigCommandHandler> _logger;
    private readonly TimeProvider _time;
    private readonly IRedisBusinessConfig _redisBusinessConfig;

    public ResetBusinessConfigCommandHandler(
        IBusinessRepository businessRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<ResetBusinessConfigCommandHandler> logger,
        IRedisBusinessConfig redisBusinessConfig,
        TimeProvider time)
    {
        _businessRepository = businessRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _redisBusinessConfig = redisBusinessConfig;
        _time = time;
    }

    public async Task<Result<BusinessConfigResponse>> Handle(
        ResetBusinessConfigCommand request,
        CancellationToken cancellationToken)
    {
        var currentBusinessResult = await _currentUserService.GetBusiness();
        if (!currentBusinessResult.IsSuccess)
        {
            return Result<BusinessConfigResponse>.Failure(
                currentBusinessResult.StatusCode,
                currentBusinessResult.Message,
                currentBusinessResult.Errors,
                currentBusinessResult.MessageCode);
        }

        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<BusinessConfigResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors,
                currentUserResult.MessageCode);
        }

        var business = currentBusinessResult.Data!;
        business.Config = new SmartShoppingChatBot.Domain.Entities.BusinessConfig();
        business.UpdatedAt = _time.GetUtcNow();
        business.UpdatedBy = new UserEmbedded
        {
            Id = currentUserResult.Data!.Id,
            Name = currentUserResult.Data.FullName
        };

        try
        {
            await _businessRepository.UpdateAsync(business);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _redisBusinessConfig.SetBusinessConfigAsync(business, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while resetting config for business {BusinessId}.", business.Id);
            return Result<BusinessConfigResponse>.Failure(
                500,
                "An error occurred while resetting the business config.",
                messageCode: BusinessMessageCode.ConfigDefaultFailed);
        }

        var response = _mapper.Map<BusinessConfigResponse>(business.Config);
        return Result<BusinessConfigResponse>.Success(
            response,
            200,
            "Business config reset to default successfully.",
            BusinessMessageCode.ConfigDefaultSuccess);
    }
}
