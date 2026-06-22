using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly PayOSClient _payOSClient;
        private readonly IUserRepository _userRepository;
        private readonly IBusinessRepository _businessRepository;
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IBusinessQuotaRepository _businessQuotaRepository;
        private readonly IConfiguration _config;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentService> _logger;
        private readonly IMapper mapper;
        private readonly IPaymentRepository _paymentRepository;


        public PaymentService(IUnitOfWork unitOfWork, ILogger<PaymentService> logger,
            IMapper mapper, IPaymentRepository paymentRepository, IConfiguration config,
            IUserRepository userRepository, ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository, IBusinessRepository businessRepository, IBusinessQuotaRepository businessQuotaRepository)
        {
            _payOSClient = new PayOSClient(
                config["PayOS:ClientId"]!,
                config["PayOS:ApiKey"]!,
                config["PayOS:ChecksumKey"]!
            );
            _unitOfWork = unitOfWork;
            _logger = logger;
            this.mapper = mapper;
            _config = config;
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _subscriptionPlanRepository = subscriptionPlanRepository;
            _subscriptionRepository = subscriptionRepository;
            _businessRepository = businessRepository;
            _businessQuotaRepository = businessQuotaRepository;
        }


        public async Task<Result<PaymentResponsed>> CreatePaymentLink(CreatePaymentRequest request)
        {
            if (ObjectId.TryParse(request.BussinessId, out var businessId) == false)
            {
                return Result<PaymentResponsed>.Failure(400, "Invalid business id format");
            }
            if (ObjectId.TryParse(request.SubscriptionPlanId, out var subscriptionPlanId) == false)
            {
                return Result<PaymentResponsed>.Failure(400, "Invalid subscription plan id format");
            }
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Random.Shared.Next(1000, 9999);
            var business = await _businessRepository.FindAsync(x => x.Id == businessId && x.BusinessStatus == BusinessEnums.ACTIVE);
            if (business == null)
            {
                return Result<PaymentResponsed>.Failure(404, "Business not found");
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == subscriptionPlanId && x.Status == StatusEnums.Active);
            if (subscriptionPlan == null)
            {
                return Result<PaymentResponsed>.Failure(404, "Subscription plan not found");
            }
            string returnBaseUrl = !string.IsNullOrEmpty(request.ReturnUrlDomain) ? request.ReturnUrlDomain : _config["PayOS:Url"]!;
            var formatPrice = Math.Floor(subscriptionPlan.Price);
            var paymentRequest = new CreatePaymentLinkRequest
            {
                Amount = (long)formatPrice,
                OrderCode = orderCode,
                ReturnUrl = $"{returnBaseUrl}/payment-success?orderCode={orderCode}",
                CancelUrl = $"{returnBaseUrl}/payment-cancel?orderCode={orderCode}",
                Description = $"{subscriptionPlan.Name} {formatPrice} VND"
            };
            var paymentId = ObjectId.GenerateNewId();
            var payment = new Payment
            {
                Id = paymentId,
                BussinessId = businessId,
                SubscriptionPlanId = subscriptionPlanId,
                OrderCode = orderCode,
                Amount = formatPrice,
                Description = $"{subscriptionPlan.Name} {formatPrice} VND",
                Status = PaymentEnums.Pending,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var result = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
                await _paymentRepository.AddAsync(payment);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                return Result<PaymentResponsed>.Success(new PaymentResponsed
                {
                    CheckoutUrl = result.CheckoutUrl,
                    OrderCode = orderCode,
                    Message = "Payment link created successfully"
                }, 200, "Payment link created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment link");
                await _unitOfWork.RollBackAsync();
                return Result<PaymentResponsed>.Failure(500, "An error occurred while creating the payment link");
            }
        }

        public async Task<bool> VerifyPaymentWebhook(PayOSWebhookRequest webhookData)
        {
            // Verify the webhook signature and data
            var data = await _payOSClient.Webhooks.VerifyAsync(new PayOS.Models.Webhooks.Webhook
            {
                Code = webhookData.Code,
                Description = webhookData.Desc,
                Signature = webhookData.Signature,
                Success = webhookData.Success,
                Data = new PayOS.Models.Webhooks.WebhookData
                {
                    Amount = webhookData.Data.Amount,
                    OrderCode = webhookData.Data.OrderCode,
                    Description = webhookData.Data.Description,
                    TransactionDateTime = webhookData.Data.TransactionDateTime,
                },
            });

            // Validate webhook verification result
            if (data == null)
            {
                _logger.LogWarning("Webhook verification returned null for order code {OrderCode}", webhookData.Data.OrderCode);
                return false;
            }

            if (!webhookData.Success)
            {
                _logger.LogWarning("Webhook success flag is false for order code {OrderCode}", webhookData.Data.OrderCode);
                return false;
            }

            //check if payment with order code exists
            var existingPayment = await _paymentRepository.FindAsync(x => x.OrderCode == webhookData.Data.OrderCode);
            if (existingPayment == null)
            {
                _logger.LogWarning("Payment with order code {OrderCode} not found", webhookData.Data.OrderCode);
                return false;
            }
            if (existingPayment.Status == PaymentEnums.Completed)
            {
                _logger.LogWarning("Payment with order code {OrderCode} has already been completed", webhookData.Data.OrderCode);
                return false;
            }

            //load subscription plan
            if (existingPayment.SubscriptionPlanId == ObjectId.Empty)
            {
                _logger.LogWarning("Payment with order code {OrderCode} has no subscription plan id", webhookData.Data.OrderCode);
                return false;
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == existingPayment.SubscriptionPlanId);
            if (subscriptionPlan == null)
            {
                _logger.LogWarning("Subscription plan with id {SubscriptionPlanId} not found", existingPayment.SubscriptionPlanId);
                return false;
            }

            //add new subscription if payment is successful
            var businessSubscription = await _subscriptionRepository
                .FindAsync(x => x.BusinessId == existingPayment.BussinessId && x.EndDate > DateTimeOffset.UtcNow && x.Status == StatusEnums.Active);
            if (businessSubscription != null)
            {
                _logger.LogWarning("Business with id {BusinessId} already has an active subscription for subscription plan id {SubscriptionPlanId}"
                    , existingPayment.BussinessId, existingPayment.SubscriptionPlanId);
                return false;
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                switch (data.Code)
                {
                    case "00":
                        // Only create subscription and quota for successful payments
                        var now = DateTimeOffset.UtcNow;
                        var businessSubscriptionId = ObjectId.GenerateNewId();
                        var subscription = new BusinessSubscription
                        {
                            Id = businessSubscriptionId,
                            BusinessId = existingPayment.BussinessId,
                            SubscriptionPlanId = existingPayment.SubscriptionPlanId,
                            StartDate = now,
                            EndDate = now.AddDays(subscriptionPlan.Duration),
                            Status = StatusEnums.Active
                        };

                        var businessQuota = new BusinessQuota
                        {
                            Id = ObjectId.GenerateNewId(),
                            BusinessId = existingPayment.BussinessId,
                            BusinessSubscriptionId = businessSubscriptionId,
                            TokenLimit = subscriptionPlan.TokenLimit,
                            MessageLimit = subscriptionPlan.MessageLimit,
                            ResetDate = now.AddDays(subscriptionPlan.Duration),
                            UsedMessages = 0,
                            UsedTokens = 0
                        };

                        existingPayment.Status = PaymentEnums.Completed;
                        await _subscriptionRepository.AddAsync(subscription);
                        await _businessQuotaRepository.AddAsync(businessQuota);
                        break;
                    case "01":
                        existingPayment.Status = PaymentEnums.Failed;
                        break;
                    case "02":
                        existingPayment.Status = PaymentEnums.Pending;
                        break;
                    default:
                        _logger.LogWarning("Unhandled webhook code: {Code}", data.Code);
                        break;
                }
                await _paymentRepository.UpdateAsync(existingPayment);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment webhook");
                await _unitOfWork.RollBackAsync();
                return false;
            }
        }
        public async Task<Result<Payment>> TestPaymentSuccessful(long orderCode)
        {

            //check if payment with order code exists
            var existingPayment = await _paymentRepository.FindAsync(x => x.OrderCode == orderCode);
            if (existingPayment == null)
            {
                _logger.LogWarning("Payment with order code {OrderCode} not found", orderCode);
                return Result<Payment>.Failure(400, "Payment not found");
            }
            if (existingPayment.Status == PaymentEnums.Completed)
            {
                _logger.LogWarning("Payment with order code {OrderCode} has already been completed", orderCode);
                return Result<Payment>.Failure(400, "Payment has already been completed");
            }

            //load subscription plan
            if (existingPayment.SubscriptionPlanId == ObjectId.Empty)
            {
                _logger.LogWarning("Payment with order code {OrderCode} has no subscription plan id", orderCode);
                return Result<Payment>.Failure(400, "Payment has no subscription plan");
            }
            var subscriptionPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == existingPayment.SubscriptionPlanId);
            if (subscriptionPlan == null)
            {
                _logger.LogWarning("Subscription plan with id {SubscriptionPlanId} not found", existingPayment.SubscriptionPlanId);
                return Result<Payment>.Failure(400, "Subscription plan not found");
            }

            //add new subscription if payment is successful
            var businessSubscription = await _subscriptionRepository
                .FindAsync(x => x.BusinessId == existingPayment.BussinessId && x.EndDate > DateTime.UtcNow && x.Status == StatusEnums.Active);
            if (businessSubscription != null)
            {
                _logger.LogWarning("Business with id {BusinessId} already has an active subscription for subscription plan id {SubscriptionPlanId}", existingPayment.BussinessId, existingPayment.SubscriptionPlanId);
                return Result<Payment>.Failure(400, "Business already has an active subscription for this subscription plan");
            }
            var now = DateTime.UtcNow;
            var businessSubscriptionId = ObjectId.GenerateNewId();
            var subscription = new BusinessSubscription
            {
                Id = businessSubscriptionId,
                BusinessId = existingPayment.BussinessId,
                SubscriptionPlanId = existingPayment.SubscriptionPlanId,
                StartDate = now,
                EndDate = now.AddDays(subscriptionPlan.Duration),
                Status = StatusEnums.Active
            };

            var businessQuota = new BusinessQuota
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = existingPayment.BussinessId,
                BusinessSubscriptionId = businessSubscriptionId,
                TokenLimit = subscriptionPlan.TokenLimit,
                MessageLimit = subscriptionPlan.MessageLimit,
                ResetDate = now.AddDays(subscriptionPlan.Duration),
                UsedMessages = 0,
                UsedTokens = 0
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                if (existingPayment.Status == PaymentEnums.Pending)
                {

                    existingPayment.Status = PaymentEnums.Completed;
                    await _subscriptionRepository.AddAsync(subscription);
                    await _businessQuotaRepository.AddAsync(businessQuota);
                    await _paymentRepository.UpdateAsync(existingPayment);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitTransactionAsync();
                    return Result<Payment>.Success(existingPayment);
                }
                else
                {
                    _logger.LogWarning("Payment with order code {OrderCode} is not in pending status", orderCode);
                    return Result<Payment>.Failure(400, "Payment is not in pending status");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing payment successful");
                await _unitOfWork.RollBackAsync();
                return Result<Payment>.Failure(500, "An error occurred while testing payment successful");
            }
        }
    }
}
