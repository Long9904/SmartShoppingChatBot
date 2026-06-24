using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CreateSubscription
{
    public class SubscriptionAddCommandHandler : IRequestHandler<SubscriptionAddCommand, Result<SubscriptionResponse>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SubscriptionAddCommandHandler> _logger;
        private readonly TimeProvider _time;
        private readonly IMapper _mapper;

        public SubscriptionAddCommandHandler(ISubscriptionPlanRepository subscriptionRepository, 
            IUnitOfWork unitOfWork, ILogger<SubscriptionAddCommandHandler> logger, TimeProvider time, IMapper mapper)
        {
            _subscriptionRepository = subscriptionRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _time = time;
            _mapper = mapper;
        }

        public async Task<Result<SubscriptionResponse>> Handle(SubscriptionAddCommand request, CancellationToken cancellationToken)
        {

            var existingSubscription = await _subscriptionRepository.FindAsync(s => s.Name == request.Name);
            if (existingSubscription != null)
            {
                return Result<SubscriptionResponse>.Failure(400, "A subscription with this name already exists.");
            }

            var subscriptionId = ObjectId.GenerateNewId();
            var subscription = new SubscriptionPlan
            {
                Id = subscriptionId,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Duration = request.Duration,
                TokenLimit = request.TokenLimit,
                MessageLimit = request.MessageLimit,
                MaxProductAllowed = request.MaxProductAllowed,
                Status = Domain.Enums.StatusEnums.Active,
            };

            await _subscriptionRepository.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();
            return Result<SubscriptionResponse>.Success(_mapper.Map<SubscriptionResponse>(subscription), 201, "Subscription plan created successfully.");
        }
    }
}
