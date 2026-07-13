using AutoMapper;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPayment
{
    public class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, Result<BasePaginatedList<PaymentResponse>>>
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBusinessRepository _businessRepository;
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly IMapper _mapper;

        public GetPaymentQueryHandler(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork,
            IBusinessRepository businessRepository, ISubscriptionPlanRepository subscriptionPlanRepository, IMapper mapper)
        {
            _paymentRepository = paymentRepository;
            _unitOfWork = unitOfWork;
            _businessRepository = businessRepository;
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _mapper = mapper;
        }

        public async Task<Result<BasePaginatedList<PaymentResponse>>> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
        {
            var query = _paymentRepository.AsQueryable();
            if (!string.IsNullOrEmpty(request.Filter.Search))
            {
                query = query.Where(p => p.Description.Contains(request.Filter.Search));
            }
            if (request.Filter.PaymentEnums != null)
            {
                query = query.Where(p => p.Status == request.Filter.PaymentEnums);
            }
            if (request.Filter.CreateAtOrderBy != null)
            {
                switch (request.Filter.CreateAtOrderBy)
                {
                    case "asc":
                        query = query.OrderBy(p => p.CreatedAt);
                        break;
                    case "desc":
                        query = query.OrderByDescending(p => p.CreatedAt);
                        break;
                }
            }
            var pagingList = await _paymentRepository.PaginatedListAsync(
                query,
                request.Filter?.PageIndex ?? 1,
                request.Filter?.PageSize ?? 10);

            var paymentList = pagingList.Items.ToList();
            var response = _mapper.Map<List<PaymentResponse>>(paymentList);

            var businessIds = paymentList.Select(p => p.BussinessId).Distinct().ToList();
            var planIds = paymentList.Select(p => p.SubscriptionPlanId).Distinct().ToList();

            var businesses = (await _businessRepository.FindAllAsync(b => businessIds.Contains(b.Id)))
                .ToDictionary(b => b.Id.ToString());
            var plans = (await _subscriptionPlanRepository.FindAllAsync(sp => planIds.Contains(sp.Id)))
                .ToDictionary(sp => sp.Id.ToString());

            foreach (var paymentResponse in response)
            {
                var payment = paymentList.FirstOrDefault(p => p.Id.ToString() == paymentResponse.Id);
                if (payment != null)
                {
                    if (businesses.TryGetValue(payment.BussinessId.ToString(), out var business))
                    {
                        paymentResponse.Bussiness = _mapper.Map<BusinessResponseV1>(business);
                    }

                    if (plans.TryGetValue(payment.SubscriptionPlanId.ToString(), out var plan))
                    {
                        paymentResponse.SubscriptionPlan = _mapper.Map<PlanResponse>(plan);
                    }
                }
            }

            return Result<BasePaginatedList<PaymentResponse>>.Success(new BasePaginatedList<PaymentResponse>
            {
                Items = response,
                TotalItems = pagingList.TotalItems,
                PageIndex = pagingList.PageIndex,
                PageSize = pagingList.PageSize
            });
        }
    }
}