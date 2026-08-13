using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.SummaryDashboard
{
    public class SummaryDashboardQueryHandler : IRequestHandler<SummaryDashboardQuery, Result<SummaryResponse>>
    {
        private readonly IBusinessRepository _businessRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;

        public SummaryDashboardQueryHandler(
            IBusinessRepository businessRepository,
            IUserRepository userRepository,
            IProductRepository productRepository,
            IKnowledgeDocumentRepository documentRepository,
            IConversationRepository conversationRepository,
            IMessageRepository messageRepository,
            ISubscriptionRepository subscriptionRepository,
            ISubscriptionPlanRepository subscriptionPlanRepository)
        {
            _businessRepository = businessRepository;
            _userRepository = userRepository;
            _productRepository = productRepository;
            _documentRepository = documentRepository;
            _conversationRepository = conversationRepository;
            _messageRepository = messageRepository;
            _subscriptionRepository = subscriptionRepository;
            _subscriptionPlanRepository = subscriptionPlanRepository;
        }

        public async Task<Result<SummaryResponse>> Handle(SummaryDashboardQuery request, CancellationToken cancellationToken)
        {
            try
            {
                // Get all data from repositories
                var businesses = _businessRepository.AsQueryable().ToList();
                var users = _userRepository.AsQueryable().ToList();
                var products = _productRepository.AsQueryable().ToList();
                var documents = _documentRepository.AsQueryable().ToList();
                var conversations = _conversationRepository.AsQueryable().ToList();
                var messages = _messageRepository.AsQueryable().ToList();
                var subscriptions = _subscriptionRepository.AsQueryable().ToList();
                var subscriptionPlans = _subscriptionPlanRepository.AsQueryable().ToList();

                // Calculate summary metrics
                var totalBusiness = businesses.Count();
                var activeBusiness = businesses.Count(b => b.BusinessStatus == BusinessEnums.ACTIVE);
                var totalUsers = users.Count();
                var totalProduct = products.Count();
                var totalDocument = documents.Count();
                var totalChatSession = conversations.Count();
                var totalMessage = messages.Count();

                // Calculate total tokens used (count all messages as each message uses tokens)
                var totalTokenUsed = messages.Count();

                // Calculate total revenue from active subscriptions
                var totalRevenue = subscriptions
                    .Where(s => s.Status == StatusEnums.Active)
                    .Join(subscriptionPlans,
                        s => s.SubscriptionPlanId,
                        p => p.Id,
                        (s, p) => p.Price)
                    .Sum(price => (int)price);

                // Count active subscriptions
                var activeSubscriptionCount = subscriptions
                    .Count(s => s.Status == StatusEnums.Active);

                // Create response
                var response = new SummaryResponse
                {
                    TotalBusiness = totalBusiness,
                    ActiveBusiness = activeBusiness,
                    TotalUsers = totalUsers,
                    TotalProduct = totalProduct,
                    TotalDocument = totalDocument,
                    TotalChatSession = totalChatSession,
                    TotalMessage = totalMessage,
                    TotalTokenUsed = totalTokenUsed,
                    TotalRevenue = totalRevenue,
                    ActiveSubscriptionCount = activeSubscriptionCount
                };

                return Result<SummaryResponse>.Success(response,200,"Summary dashboard data retrieved successfully.");
            }
            catch (Exception ex)
            {
                return Result<SummaryResponse>.Failure(
                    500,
                    $"An error occurred while processing the summary dashboard: {ex.Message}");
            }
        }
    }
}
