using AutoMapper;
using Google.Cloud.AIPlatform.V1;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System.Text.Json;


namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.UpdateSubscription
{
    public class SubscriptionUpdateCommandHandler : IRequestHandler<SubscriptionUpdateCommand, Result<SubscriptionResponse>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SubscriptionUpdateCommandHandler> _logger;
        private readonly TimeProvider _time;
        private readonly IMapper _mapper;
        private readonly IActivityLogService _activityLogService;

        public SubscriptionUpdateCommandHandler(ISubscriptionPlanRepository subscriptionRepository,
            IUnitOfWork unitOfWork, ILogger<SubscriptionUpdateCommandHandler> logger, TimeProvider time, IMapper mapper, IActivityLogService activityLogService)
        {
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _time = time;
            _mapper = mapper;
            _activityLogService = activityLogService;
            _mapper = mapper;
        }

        public async Task<Result<SubscriptionResponse>> Handle(SubscriptionUpdateCommand request, CancellationToken cancellationToken)
        {
            if (ObjectId.TryParse(request.Id, out var subscriptionId) == false)
            {
                return Result<SubscriptionResponse>.Failure(400, "Invalid subscription ID format.");
            }

            var existingSubscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (existingSubscription == null)
            {
                return Result<SubscriptionResponse>.Failure(404, "Subscription plan not found.");
            }
            var subscriptionWithSameName = await _subscriptionRepository.FindAsync(x => x.Name == request.Name && x.Id != subscriptionId);
            if (subscriptionWithSameName != null)
            {
                return Result<SubscriptionResponse>.Failure(409, "A subscription plan with the same name already exists.");
            }
            var isUpdate = false;
            if (!string.IsNullOrEmpty(request.Name) && existingSubscription.Name != request.Name)
            {
                existingSubscription.Name = request.Name;
                isUpdate = true;
            }
            if (!string.IsNullOrEmpty(request.Description) && existingSubscription.Description != request.Description)
            {
                existingSubscription.Description = request.Description;
                isUpdate = true;
            }
            if (request.Price >= 0 && existingSubscription.Price != request.Price)
            {
                existingSubscription.Price = request.Price;
                isUpdate = true;
            }
            if (request.Duration > 0 && existingSubscription.Duration != request.Duration)
            {
                existingSubscription.Duration = request.Duration;
                isUpdate = true;
            }
            if (request.TokenLimit >= 0 && existingSubscription.TokenLimit != request.TokenLimit)
            {
                existingSubscription.TokenLimit = request.TokenLimit;
                isUpdate = true;
            }
            if (request.MessageLimit >= 0 && existingSubscription.MessageLimit != request.MessageLimit)
            {
                existingSubscription.MessageLimit = request.MessageLimit;
                isUpdate = true;
            }
            if (request.MaxProductAllowed >= 0 && existingSubscription.MaxProductAllowed != request.MaxProductAllowed)
            {
                existingSubscription.MaxProductAllowed = request.MaxProductAllowed;
                isUpdate = true;
            }
            if (request.MaxDocumentAllowed >= 0 && existingSubscription.MaxDocumentAllowed != request.MaxDocumentAllowed)
            {
                existingSubscription.MaxDocumentAllowed = request.MaxDocumentAllowed;
                isUpdate = true;
            }
            if (request.Level > 0 && existingSubscription.Level != request.Level)
            {
                existingSubscription.Level = request.Level;
                isUpdate = true;
            }
            if (!isUpdate)
            {
                return Result<SubscriptionResponse>.Success(_mapper.Map<SubscriptionResponse>(existingSubscription), 200, "No changes to update.");
            }
            await _subscriptionRepository.UpdateAsync(existingSubscription);
            await _unitOfWork.SaveChangesAsync();
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                Action = ActionLogEnums.Update,
                TargetType = nameof(existingSubscription),
                TargetId = existingSubscription.Id.ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = $"Subscription plan '{existingSubscription.Name}' updated successfully.",
                Metadata = new Dictionary<string, object?>
                {
                    { "SubscriptionId", existingSubscription.Id.ToString() },
                    { "UpdatedFields", request }
                }
            });
            return Result<SubscriptionResponse>.Success(_mapper.Map<SubscriptionResponse>(existingSubscription), 200, "Subscription plan updated successfully.");
        }
    }
}

