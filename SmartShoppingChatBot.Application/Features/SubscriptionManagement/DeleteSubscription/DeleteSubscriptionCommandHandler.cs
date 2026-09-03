using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.DeleteSubscription
{
    public class DeleteSubscriptionCommandHandler : IRequestHandler<DeleteSubscriptionCommand, Result<string>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActivityLogService _activityLogService;
        public DeleteSubscriptionCommandHandler(ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository, IUnitOfWork unitOfWork, IActivityLogService activityLogService)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _activityLogService = activityLogService;
        }

        public async Task<Result<string>> Handle(DeleteSubscriptionCommand request, CancellationToken cancellationToken)
        {
            if (ObjectId.TryParse(request.Id, out var id) == false)
            {
                return Result<string>.Failure(400, "Invalid subscription ID format.");
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == id && x.Status == Domain.Enums.StatusEnums.Active);
            if (subscriptionPlan == null)
            {
                return Result<string>.Failure(404, "Subscription not found.");
            }
            var subscriptionInOtherBusiness = await _subscriptionRepository.FindAsync(x => x.SubscriptionPlanId == subscriptionPlan.Id && x.Status == Domain.Enums.StatusEnums.Active);
            if (subscriptionInOtherBusiness != null)
            {
                return Result<string>.Failure(400, "Cannot delete subscription with active plans.");
            }
            subscriptionPlan.Status = Domain.Enums.StatusEnums.Inactive;
            await _subscriptionPlanRepository.UpdateAsync(subscriptionPlan);
            await _unitOfWork.SaveChangesAsync();
            await _activityLogService.LogAsync(new DTOs.ActivityLogRequest
            {
                Action = Domain.Enums.ActionLogEnums.Delete,
                TargetType = nameof(Domain.Entities.SubscriptionPlan),
                TargetId = subscriptionPlan.Id.ToString(),
                Status = Domain.Enums.StatusLogEnums.Success,
                Severity = Domain.Enums.SeverityLogEnums.Info,
                Description = $"Subscription plan '{subscriptionPlan.Name}' deleted successfully.",
            });
            return Result<string>.Success("Subscription deleted successfully.");
        }
    }
}

