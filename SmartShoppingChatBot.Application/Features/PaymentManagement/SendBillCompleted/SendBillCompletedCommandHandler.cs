using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.SendBillCompleted
{
    public class SendBillCompletedCommandHandler : IRequestHandler<SendBillCompletedCommand, Result<string>>
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserRepository _userRepository;
        private readonly IBusinessRepository _businessRepository;
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ILogger<SendBillCompletedCommandHandler> _logger;
        private readonly IEmailTemplateService _templateService;

        public SendBillCompletedCommandHandler(IPaymentRepository paymentRepository, IEmailService emailService,
            IUnitOfWork unitOfWork, ISubscriptionPlanRepository subscriptionPlanRepository, ILogger<SendBillCompletedCommandHandler> logger
            , IEmailTemplateService templateService, IUserRepository userRepository, IBusinessRepository businessRepository, ISubscriptionRepository subscriptionRepository )
        {
            _paymentRepository = paymentRepository;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _logger = logger;
            _templateService = templateService;
            _userRepository = userRepository;
            _businessRepository = businessRepository;
            _subscriptionRepository = subscriptionRepository;
        }

        public async Task<Result<string>> Handle(SendBillCompletedCommand request, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(request.PaymentId, out ObjectId paymentId))
            {
                return Result<string>.Failure(400, "Invalid Payment Id");
            }
            var payment = await _paymentRepository.FindAsync(x => x.Id == paymentId && x.Status == PaymentEnums.Completed);
            if (payment == null)
            {
                return Result<string>.Failure(404, "Payment not found or not completed");
            }
            var business = await _businessRepository.FindAsync(x => x.Id == payment.BussinessId);
            if (business == null)
            {
                return Result<string>.Failure(404, "Business not found");
            }
            var user = await _userRepository.FindAsync(x => x.Business.Id == payment.BussinessId);
            if(user  == null)
            {
                return Result<string>.Failure(404, "Business not found");
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == payment.SubscriptionPlanId && x.Status == StatusEnums.Active);
            if (subscriptionPlan == null)
            {
                return Result<string>.Failure(404, "Subscription plan not found or not active");
            }
            var subscription = await _subscriptionRepository.FindAsync(x => x.SubscriptionPlanId == payment.SubscriptionPlanId);
            if (subscription == null)
            {
                return Result<string>.Failure(404, "Subscription not found");
            }
            // Generate the email content using the template service
            var emailContent = await _templateService.RenderEmailTemplateAsync("BillCompleted", new BillCompletedEmailModel
            {
                BusinessName = business.BusinessName,
                OrderCode = payment.OrderCode,
                Amount = payment.Amount,
                PaidAt = payment.CreatedAt,
                PaymentMethod = "Bank Transfer",
                PaymentStatus = payment.Status.ToString(),
                SubscriptionName = subscriptionPlan.Name,
                SubscriptionStartDate = subscription.StartDate,
                SubscriptionEndDate = subscription.EndDate,
                SupportEmail ="support@lunarAI.com",
                InvoiceId = payment.Id.ToString(),
                InvoiceUrl = $"https://lunarai.com/invoice/{payment.Id}"

            });
            // Send the email to the user with the bill details
            await _emailService.SendEmailAsync(user.Email, "Bill Completed", emailContent);
            return Result<string>.Success("Bill sent successfully");
        }
    }
}
