using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.GetAllSubscription
{
    public class GetSubscriptionQueryHandle : IRequestHandler<GetSubscriptionQuery, Result<BasePaginatedList<SubscriptionResponse>>>
    {
        private readonly ISubscriptionPlanRepository _subscriptionRepository;
        private readonly IMapper _mapper;
        public GetSubscriptionQueryHandle(ISubscriptionPlanRepository subscriptionRepository, IMapper mapper)
        {
            _subscriptionRepository = subscriptionRepository;
            _mapper = mapper;
        }
        public async Task<Result<BasePaginatedList<SubscriptionResponse>>> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
        {
            var subscriptionPlans = _subscriptionRepository.AsQueryable();
            if(!string.IsNullOrEmpty(request.Filter?.Search))
            {
                subscriptionPlans = subscriptionPlans.Where(s => s.Name.Contains(request.Filter.Search));
            }
            if(request.Filter?.Status.HasValue == true)
            {
                subscriptionPlans = subscriptionPlans.Where(s => s.Status == request.Filter.Status);
            }
            var pagingList = await _subscriptionRepository.PaginatedListAsync(subscriptionPlans,request.Filter?.PageIndex ?? 1,request.Filter?.PageSize ?? 10);
            var responseItems = _mapper.Map<IReadOnlyCollection<SubscriptionResponse>>(pagingList.Items);

            return Result<BasePaginatedList<SubscriptionResponse>>.Success(new BasePaginatedList<SubscriptionResponse>
            {
                Items = responseItems,
                TotalItems = pagingList.TotalItems,
                TotalPages = pagingList.TotalPages,
                PageIndex = pagingList.PageIndex,
                PageSize = pagingList.PageSize
            });
        }
    }
}
