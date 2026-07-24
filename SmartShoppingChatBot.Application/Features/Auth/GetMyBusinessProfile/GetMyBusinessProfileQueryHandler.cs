using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.Auth.GetMyBusinessProfile;

public class GetMyBusinessProfileQueryHandler : IRequestHandler<GetMyBusinessProfileQuery, Result<MyBusinessProfileResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly IBusinessQuotaRepository _businessQuotaRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IMapper _mapper;

    public GetMyBusinessProfileQueryHandler(
        ICurrentUserService currentUserService,
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IBusinessQuotaRepository businessQuotaRepository,
        TimeProvider timeProvider,
        IMapper mapper)
    {
        _currentUserService = currentUserService;
        _subscriptionRepository = subscriptionRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _businessQuotaRepository = businessQuotaRepository;
        _timeProvider = timeProvider;
        _mapper = mapper;
    }

    public async Task<Result<MyBusinessProfileResponse>> Handle(
        GetMyBusinessProfileQuery request,
        CancellationToken cancellationToken)
    {
        var currentBusinessResult = await _currentUserService.GetBusiness();
        if (!currentBusinessResult.IsSuccess)
        {
            return Result<MyBusinessProfileResponse>.Failure(
                currentBusinessResult.StatusCode,
                currentBusinessResult.Message,
                currentBusinessResult.Errors);
        }

        var business = currentBusinessResult.Data!;
        var response = _mapper.Map<MyBusinessProfileResponse>(business);
        var now = _timeProvider.GetUtcNow();

        var currentSubscription = await _subscriptionRepository.FindAsync(
            subscription =>
                subscription.BusinessId == business.Id
                && subscription.Status == StatusEnums.Active
                && subscription.StartDate <= now
                && subscription.EndDate > now);

        if (currentSubscription != null)
        {
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(
                plan => plan.Id == currentSubscription.SubscriptionPlanId);

            if (subscriptionPlan != null)
            {
                response.CurrentSubscription = new CurrentBusinessSubscriptionResponse
                {
                    PlanId = subscriptionPlan.Id.ToString(),
                    PlanName = subscriptionPlan.Name,
                    StartDate = currentSubscription.StartDate,
                    EndDate = currentSubscription.EndDate
                };
            }

            var businessQuota = await _businessQuotaRepository.FindAsync(
                quota =>
                    quota.BusinessId == business.Id
                    && quota.BusinessSubscriptionId == currentSubscription.Id);

            if (businessQuota != null)
            {
                response.BusinessQuota = new BusinessQuotaResponse
                {
                    Id = businessQuota.Id.ToString(),
                    BusinessSubscriptionId = businessQuota.BusinessSubscriptionId.ToString(),
                    TokenLimit = businessQuota.TokenLimit,
                    MessageLimit = businessQuota.MessageLimit,
                    UsedTokens = businessQuota.UsedTokens,
                    UsedMessages = businessQuota.UsedMessages,
                    MaxProductAllowed = businessQuota.MaxProductAllowed,
                    ResetDate = businessQuota.ResetDate
                };
            }
        }

        return Result<MyBusinessProfileResponse>.Success(
            response,
            200,
            "Business profile retrieved successfully.");
    }
}
