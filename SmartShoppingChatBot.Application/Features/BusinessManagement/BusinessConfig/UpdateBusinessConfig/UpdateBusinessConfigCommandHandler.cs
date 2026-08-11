using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.UpdateBusinessConfig;

public class UpdateBusinessConfigCommandHandler
    : IRequestHandler<UpdateBusinessConfigCommand, Result<BusinessConfigResponse>>
{
    private readonly IBusinessRepository _businessRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateBusinessConfigCommandHandler> _logger;
    private readonly TimeProvider _time;
    private readonly IRedisBusinessConfig _redisBusinessConfig;

    public UpdateBusinessConfigCommandHandler(
        IBusinessRepository businessRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateBusinessConfigCommandHandler> logger,
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
        UpdateBusinessConfigCommand request,
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

        var business = currentBusinessResult.Data!;
        if (business.Config == null)
        {
            return Result<BusinessConfigResponse>.Failure(
                404,
                "Business config not found.",
                messageCode: BusinessMessageCode.ConfigNotFound);
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

        business.Config.ModelTemperature = request.ModelTemperature;
        business.Config.TopKDocument = request.TopKDocument;
        business.Config.RerankingScore = request.RerankingScore;
        business.Config.SystemPrompt = request.SystemPrompt?.Trim() ?? string.Empty;
        business.Config.FallBackMessage = request.FallBackMessage?.Trim() ?? string.Empty;
        business.Config.MaxOutPutToken = request.MaxOutPutToken;
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
            _logger.LogError(ex, "An error occurred while updating config for business {BusinessId}.", business.Id);
            return Result<BusinessConfigResponse>.Failure(
                500,
                "An error occurred while updating the business config.",
                messageCode: BusinessMessageCode.ConfigUpdateFailed);
        }

        var response = _mapper.Map<BusinessConfigResponse>(business.Config);
        return Result<BusinessConfigResponse>.Success(
            response,
            200,
            "Business config updated successfully.",
            BusinessMessageCode.ConfigUpdateSuccess);
    }
}
