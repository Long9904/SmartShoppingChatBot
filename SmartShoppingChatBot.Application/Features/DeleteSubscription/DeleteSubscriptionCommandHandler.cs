using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DeleteSubscription
{
    public class DeleteSubscriptionCommandHandler : IRequestHandler<DeleteSubscriptionCommand, Result<string>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        public DeleteSubscriptionCommandHandler(ISubscriptionPlanRepository subscriptionPlanRepository, IUnitOfWork unitOfWork)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> Handle(DeleteSubscriptionCommand request, CancellationToken cancellationToken)
        {
            if (ObjectId.TryParse(request.Id, out var id) == false)
            {
                return Result<string>.Failure(400, "Invalid subscription ID format.");
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == id && x.Status == Domain.Enums.StatusEnums.Inactive);
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
            return Result<string>.Success("Subscription deleted successfully.");
        }
    }
}

