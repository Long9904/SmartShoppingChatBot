using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.SubscriptionsDashboard
{
    public class SubscriptionDashboardQueryHandler : IRequestHandler<SubscriptionDashboardQuery, Result<BasePaginatedList<SubscriptionDashboardResponse>>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IBusinessRepository _businessRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public SubscriptionDashboardQueryHandler(ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository, IBusinessRepository businessRepository, ICurrentUserService currentUserService, IMapper mapper)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _subscriptionRepository = subscriptionRepository;
            _businessRepository = businessRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<Result<BasePaginatedList<SubscriptionDashboardResponse>>> Handle(SubscriptionDashboardQuery request, CancellationToken cancellationToken)
        {
            var subscriptionPlans = _subscriptionPlanRepository.AsQueryable();
            if (request.Filter.Status != null)
            {
                subscriptionPlans = subscriptionPlans.Where(d => d.Status == request.Filter.Status);
            }
            var totalBusiness = _subscriptionRepository.AsQueryable().Count();
            var pagingList = await _subscriptionPlanRepository.PaginatedListAsync(subscriptionPlans, request.Filter?.PageIndex ?? 1, request.Filter?.PageSize ?? 10);
            var responseItems = pagingList.Items.Select(x =>
            {
                var businessCount = _subscriptionRepository
                    .AsQueryable()
                    .Count(s => s.SubscriptionPlanId == x.Id);

                return new SubscriptionDashboardResponse
                {
                    Id = x.Id.ToString(),
                    Name = x.Name,
                    Status = x.Status,

                    Detail = new DetailResponse
                    {
                        BusinessCount = businessCount,
                        Rate = totalBusiness == 0
                            ? 0
                            : (double)businessCount / totalBusiness * 100
                    }
                };
            }).ToList();

            return Result<BasePaginatedList<SubscriptionDashboardResponse>>.Success(new BasePaginatedList<SubscriptionDashboardResponse>
            {
                Items = responseItems,
                TotalItems = pagingList.TotalItems,
                TotalPages = pagingList.TotalPages,
                PageIndex = pagingList.PageIndex,
                PageSize = pagingList.PageSize
            }, 200, "Get subscriptions successfully");
        }

    }


}
