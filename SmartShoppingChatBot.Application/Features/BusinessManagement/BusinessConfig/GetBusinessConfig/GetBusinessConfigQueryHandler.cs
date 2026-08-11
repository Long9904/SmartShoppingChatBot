using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.GetBusinessConfig;

public class GetBusinessConfigQueryHandler
    : IRequestHandler<GetBusinessConfigQuery, Result<BusinessConfigResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessRepository _businessRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetBusinessConfigQueryHandler> _logger;
    private readonly TimeProvider _time;

    public GetBusinessConfigQueryHandler(
        ICurrentUserService currentUserService,
        IBusinessRepository businessRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetBusinessConfigQueryHandler> logger,
        TimeProvider time)
    {
        _currentUserService = currentUserService;
        _businessRepository = businessRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<BusinessConfigResponse>> Handle(
        GetBusinessConfigQuery request,
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
            var currentUserResult = await _currentUserService.GetUser();
            if (!currentUserResult.IsSuccess)
            {
                return Result<BusinessConfigResponse>.Failure(
                    currentUserResult.StatusCode,
                    currentUserResult.Message,
                    currentUserResult.Errors,
                    currentUserResult.MessageCode);
            }

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating default config for business {BusinessId}.", business.Id);
                return Result<BusinessConfigResponse>.Failure(
                    500,
                    "An error occurred while creating the default business config.",
                    messageCode: BusinessMessageCode.ConfigGetFailed);
            }
        }

        var response = _mapper.Map<BusinessConfigResponse>(business.Config);
        return Result<BusinessConfigResponse>.Success(
            response,
            200,
            "Business config retrieved successfully.",
            BusinessMessageCode.ConfigGetSuccess);
    }
}
