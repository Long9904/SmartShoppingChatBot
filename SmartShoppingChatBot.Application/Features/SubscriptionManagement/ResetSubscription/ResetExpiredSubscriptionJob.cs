using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Quartz;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.ResetSubscription
{
    public class ResetExpiredSubscriptionJob : IJob
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IBusinessQuotaRepository _businessQuotaRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;
        private readonly ILogger<ResetExpiredSubscriptionJob> _logger;

        public ResetExpiredSubscriptionJob(
            ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository,
            IBusinessQuotaRepository businessQuotaRepository,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            ILogger<ResetExpiredSubscriptionJob> logger)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _subscriptionRepository = subscriptionRepository;
            _businessQuotaRepository = businessQuotaRepository;
            _unitOfWork = unitOfWork;
            _time = time;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("ResetExpiredSubscriptionJob started at {Time}", _time.GetUtcNow());
            var now = _time.GetUtcNow();
            var expiredSubscriptions = await _subscriptionRepository.FilterByAsync(
                subscription => subscription.EndDate <= now
                    && subscription.Status == StatusEnums.Active);

            if (expiredSubscriptions.Count == 0)
            {
                return;
            }

            var basic = await _subscriptionPlanRepository.FindAsync(
                plan => plan.Name == "Basic" && plan.Status == StatusEnums.Active);

            if (basic == null)
            {
                _logger.LogWarning("Basic subscription plan was not found. Skipping subscription expiration reset.");
                return;
            }

            var newSubscriptions = new List<BusinessSubscription>();
            var newQuotas = new List<BusinessQuota>();

            foreach (var expiredSubscription in expiredSubscriptions)
            {
                expiredSubscription.Status = StatusEnums.Inactive;
                expiredSubscription.EndDate = now;

                var newSubscriptionId = ObjectId.GenerateNewId();
                var resetDate = now.AddDays(basic.Duration);

                newSubscriptions.Add(new BusinessSubscription
                {
                    Id = newSubscriptionId,
                    BusinessId = expiredSubscription.BusinessId,
                    SubscriptionPlanId = basic.Id,
                    StartDate = now,
                    EndDate = resetDate,
                    Status = StatusEnums.Active
                });

                newQuotas.Add(new BusinessQuota
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = expiredSubscription.BusinessId,
                    BusinessSubscriptionId = newSubscriptionId,
                    TokenLimit = basic.TokenLimit,
                    MessageLimit = basic.MessageLimit,
                    UsedTokens = 0,
                    UsedMessages = 0,
                    MaxProductAllowed = basic.MaxProductAllowed,
                    ResetDate = resetDate
                });
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync(context.CancellationToken);
                await _subscriptionRepository.UpdateRangeAsync(expiredSubscriptions);
                await _subscriptionRepository.AddRangeAsync(newSubscriptions);
                await _businessQuotaRepository.AddRangeAsync(newQuotas);
                await _unitOfWork.SaveChangesAsync(context.CancellationToken);
                await _unitOfWork.CommitTransactionAsync(context.CancellationToken);
                _logger.LogInformation("ResetExpiredSubscriptionJob completed successfully at {Time}", _time.GetUtcNow());
            }
            catch (Exception exception)
            {
                await _unitOfWork.RollBackAsync(context.CancellationToken);
                _logger.LogError(exception, "Failed to reset expired subscriptions to Basic plan.");
                throw;
            }
        }
    }
}
