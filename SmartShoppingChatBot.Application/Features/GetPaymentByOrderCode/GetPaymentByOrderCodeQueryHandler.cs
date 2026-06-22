using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.GetPaymentByOrderCode
{
    public class GetPaymentByOrderCodeQueryHandler : IRequestHandler<GetPaymentByOrderCodeQuery, Result<PaymentResponse>>
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;
        private readonly IBusinessRepository _businessRepository;
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;


        public GetPaymentByOrderCodeQueryHandler(IPaymentRepository paymentRepository, IMapper mapper, ICurrentUserService currentUserService,
            IBusinessRepository businessRepository, ISubscriptionPlanRepository subscriptionPlanRepository)
        {
            _paymentRepository = paymentRepository;
            _mapper = mapper;
            _currentUserService = currentUserService;
            _businessRepository = businessRepository;
            _subscriptionPlanRepository = subscriptionPlanRepository;
        }

        public async Task<Result<PaymentResponse>> Handle(GetPaymentByOrderCodeQuery request, CancellationToken cancellationToken)
        {
            var businessLogin = await _currentUserService.GetBusiness();
            if (!businessLogin.IsSuccess)
            {
                return Result<PaymentResponse>.Failure(businessLogin.StatusCode, businessLogin.Message);
            }
            var payment = await _paymentRepository.FindAsync(x => x.OrderCode == request.OrderCode && x.BussinessId == businessLogin.Data.Id);
            if (payment == null)
            {
                return Result<PaymentResponse>.Failure(404,"Payment not found");
            }
            var response = _mapper.Map<PaymentResponse>(payment);

            var business = await _businessRepository.FindAsync(
                x => x.Id == payment.BussinessId);

            if (business != null)
            {
                response.Bussiness = _mapper.Map<BusinessResponseV1>(business);
            }

            var plan = await _subscriptionPlanRepository.FindAsync(
                x => x.Id == payment.SubscriptionPlanId);

            if (plan != null)
            {
                response.SubscriptionPlan = _mapper.Map<PlanResponse>(plan);
            }
            
            return Result<PaymentResponse>.Success(response);
        }
    }
}

