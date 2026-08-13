using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.RevenueDashboard
{
    public class RevenueDashboardQueryHandler : IRequestHandler<RevenueDashboardQuery, Result<BasePaginatedList<RevenueDashboardResponse>>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;

        public RevenueDashboardQueryHandler(
            ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _subscriptionRepository = subscriptionRepository;
        }

        public async Task<Result<BasePaginatedList<RevenueDashboardResponse>>> Handle(RevenueDashboardQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var subscriptions = _subscriptionRepository.AsQueryable().ToList();
                var subscriptionPlans = _subscriptionPlanRepository.AsQueryable().ToList();

                // Calculate total revenue (sum of all subscription prices)
                var totalRevenue = subscriptions
                    .Where(s => s.Status == StatusEnums.Active)
                    .Join(subscriptionPlans, 
                        s => s.SubscriptionPlanId, 
                        p => p.Id, 
                        (s, p) => p.Price)
                    .Sum(price => (int)price);

                // Calculate total revenue for current month
                var currentYear = DateTime.Now.Year;
                var month = request.Filter.Month > 0 ? request.Filter.Month : DateTime.Now.Month;

                var totalRevenueThisMonth = subscriptions
                    .Where(s => s.Status == StatusEnums.Active 
                        && s.StartDate.Year == currentYear 
                        && s.StartDate.Month == month)
                    .Join(subscriptionPlans,
                        s => s.SubscriptionPlanId,
                        p => p.Id,
                        (s, p) => p.Price)
                    .Sum(price => (int)price);

                // Calculate subscription counts
                var activeSubscriptionCount = subscriptions
                    .Count(s => s.Status == StatusEnums.Active);

                var totalSubscriptionCount = subscriptions.Count();

                var cancelledSubscriptionCount = subscriptions
                    .Count(s => s.Status == StatusEnums.Inactive);

                // Create response
                var response = new RevenueDashboardResponse
                {
                    TotalRevenue = totalRevenue,
                    TotalRevenueThisMonth = totalRevenueThisMonth,
                    ActiveSubscriptionCount = activeSubscriptionCount,
                    TotalSubscriptionCount = totalSubscriptionCount,
                    CancelledSubscriptionCount = cancelledSubscriptionCount
                };

                // Return paginated result
                var paginatedList = new BasePaginatedList<RevenueDashboardResponse>(
                    new List<RevenueDashboardResponse> { response },
                    1,
                    1,
                    1);

                return Result<BasePaginatedList<RevenueDashboardResponse>>.Success(paginatedList,200,"Revenue dashboard data retrieved successfully.");
            }
            catch (Exception ex)
            {
                return Result<BasePaginatedList<RevenueDashboardResponse>>.Failure(
                    500,
                    $"An error occurred while processing the revenue dashboard: {ex.Message}");
            }
        }
    }
}
