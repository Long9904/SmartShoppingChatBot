using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.BusinessRegistration;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.SubscriptionUpdate
{
    public class SubscriptionUpdateCommandHandler : IRequestHandler<SubscriptionUpdateCommand, Result<SubscriptionResponse>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SubscriptionUpdateCommandHandler> _logger;
        private readonly TimeProvider _time;
        private readonly IMapper _mapper;

        public SubscriptionUpdateCommandHandler(ISubscriptionPlanRepository subscriptionRepository,
            IUnitOfWork unitOfWork, ILogger<SubscriptionUpdateCommandHandler> logger, TimeProvider time, IMapper mapper)
        {
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _time = time;
            _mapper = mapper;
        }

        public async Task<Result<SubscriptionResponse>> Handle( SubscriptionUpdateCommand request, CancellationToken cancellationToken)
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
            if(!string.IsNullOrEmpty(request.Name) && existingSubscription.Name != request.Name)
            {
                existingSubscription.Name = request.Name;
                isUpdate = true;
            }
            if(!string.IsNullOrEmpty(request.Description) && existingSubscription.Description != request.Description)
            {
                existingSubscription.Description = request.Description;
                isUpdate = true;
            }
            if(request.Price >= 0 && existingSubscription.Price != request.Price)
            {
                existingSubscription.Price = request.Price;
                isUpdate = true;
            }
            if(request.Duration > 0 && existingSubscription.Duration != request.Duration)
            {
                existingSubscription.Duration = request.Duration;
                isUpdate = true;
            }
            if(request.TokenLimit >= 0 && existingSubscription.TokenLimit != request.TokenLimit)
            {
                existingSubscription.TokenLimit = request.TokenLimit;
                isUpdate = true;
            }
            if(request.MessageLimit >= 0 && existingSubscription.MessageLimit != request.MessageLimit)
            {
                existingSubscription.MessageLimit = request.MessageLimit;
                isUpdate = true;
            }
            if (!isUpdate)
            {
                return Result<SubscriptionResponse>.Success(_mapper.Map<SubscriptionResponse>(existingSubscription), 200, "No changes to update.");
            }
            await _subscriptionRepository.UpdateAsync(existingSubscription);
            await _unitOfWork.SaveChangesAsync();
            return Result<SubscriptionResponse>.Success(_mapper.Map<SubscriptionResponse>(existingSubscription), 200, "Subscription plan updated successfully.");
        }
    }
}

