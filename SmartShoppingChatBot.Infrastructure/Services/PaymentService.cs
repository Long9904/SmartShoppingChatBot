using AutoMapper;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models.V2.PaymentRequests;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

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
        private readonly ICurrentUserService _currentUserService;
        private readonly IPublishEndpoint _publishEndpoint;


        public PaymentService(IUnitOfWork unitOfWork, ILogger<PaymentService> logger,
            IMapper mapper, IPaymentRepository paymentRepository, IConfiguration config,
            IUserRepository userRepository, ISubscriptionPlanRepository subscriptionPlanRepository,
            ISubscriptionRepository subscriptionRepository, IBusinessRepository businessRepository,
            IBusinessQuotaRepository businessQuotaRepository, ICurrentUserService currentUserService, IPublishEndpoint publishEndpoint)
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
            _currentUserService = currentUserService;
            _publishEndpoint = publishEndpoint;
        }


        public async Task<Result<PaymentResponsed>> CreatePaymentLink(CreatePaymentRequest request)
        {
            // Validate input parameters
            var businessLogin = await _currentUserService.GetBusiness();
            if (businessLogin == null || businessLogin.Data == null)
            {
                return Result<PaymentResponsed>.Failure(404, "Business not found");
            }
            if (ObjectId.TryParse(request.SubscriptionPlanId, out var subscriptionPlanId) == false)
            {
                return Result<PaymentResponsed>.Failure(400, "Invalid subscription plan id format");
            }
            var business = await _businessRepository.FindAsync(x => x.Id == businessLogin.Data.Id && x.BusinessStatus == BusinessEnums.ACTIVE);
            if (business == null)
            {
                return Result<PaymentResponsed>.Failure(404, "Business not found");
            }
            var selectSubscription = await _subscriptionPlanRepository.FindAsync(x => x.Id == subscriptionPlanId && x.Status == StatusEnums.Active);
            if (selectSubscription == null)
            {
                return Result<PaymentResponsed>.Failure(404, "Subscription plan not found");
            }
            // Check if the business already has an active subscription
            var currentSubscription = await _subscriptionRepository
                .FindAsync(x => x.BusinessId == business.Id && x.StartDate <= DateTimeOffset.UtcNow && x.EndDate > DateTimeOffset.UtcNow && x.Status == StatusEnums.Active);
            if (currentSubscription != null)
            {
                var currentPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == currentSubscription.SubscriptionPlanId);
                if (currentPlan == null)
                {
                    return Result<PaymentResponsed>.Failure(404, "Current subscription plan not found");
                }
                if (currentPlan.Id == selectSubscription.Id)
                {
                    return Result<PaymentResponsed>.Failure(400, "Business already has an active subscription for this plan");
                }
                if (selectSubscription.Level <= currentPlan.Level)
                {
                    return Result<PaymentResponsed>.Failure(400, "The selected plan must be a higher tier than the current plan");
                }
            }
            var existingPayment = await _paymentRepository
                .FindAsync(x => x.BussinessId == business.Id && x.SubscriptionPlanId == selectSubscription.Id && x.Status == PaymentEnums.Pending);
            if (existingPayment != null)
            {
                return Result<PaymentResponsed>.Failure(400, "Business already has a pending payment for this plan");
            }

            var orderCode = GenerateOrderCode();
            string returnBaseUrl = !string.IsNullOrEmpty(request.ReturnUrlDomain) ? request.ReturnUrlDomain : _config["PayOS:Url"]!;
            var formatPrice = Math.Floor(selectSubscription.Price);
            var paymentRequest = new CreatePaymentLinkRequest
            {
                Amount = (long)formatPrice,
                OrderCode = orderCode,
                ReturnUrl = $"{returnBaseUrl}/payment-success/{orderCode}",
                CancelUrl = $"{returnBaseUrl}/payment-cancel/{orderCode}",
                Description = $"{selectSubscription.Name} {formatPrice}"
            };
            var paymentId = ObjectId.GenerateNewId();
            var payment = new Payment
            {
                Id = paymentId,
                BussinessId = business.Id,
                SubscriptionPlanId = subscriptionPlanId,
                OrderCode = orderCode,
                Amount = formatPrice,
                Description = $"{selectSubscription.Name} {formatPrice} VND",
                Status = PaymentEnums.Pending,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var result = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
                await _paymentRepository.AddAsync(payment);
                await _unitOfWork.SaveChangesAsync();
                return Result<PaymentResponsed>.Success(new PaymentResponsed
                {
                    CheckoutUrl = result.CheckoutUrl,
                    OrderCode = orderCode,
                    Message = "Payment link created successfully"
                }, 200, "Payment link created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating payment link: {ErrorMessage}", ex.Message);
                return Result<PaymentResponsed>.Failure(500, "An error occurred while creating the payment link");
            }
        }

        public async Task<bool> VerifyPaymentWebhook(PayOSWebhookRequest webhookData)
        {
            //map webhookData to PayOS.Models.Webhooks.Webhook
            var webhook = new PayOS.Models.Webhooks.Webhook
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
                    AccountNumber = webhookData.Data.AccountNumber,
                    Reference = webhookData.Data.Reference,
                    TransactionDateTime = webhookData.Data.TransactionDateTime,
                    Currency = webhookData.Data.Currency,
                    PaymentLinkId = webhookData.Data.PaymentLinkId,
                    Code = webhookData.Data.Code,
                    Description2 = webhookData.Data.Desc,
                    CounterAccountBankId = webhookData.Data.CounterAccountBankId,
                    CounterAccountBankName = webhookData.Data.CounterAccountBankName,
                    CounterAccountName = webhookData.Data.CounterAccountName,
                    CounterAccountNumber = webhookData.Data.CounterAccountNumber,
                    VirtualAccountName = webhookData.Data.VirtualAccountName,
                    VirtualAccountNumber = webhookData.Data.VirtualAccountNumber,
                },
            };
            // Verify the webhook signature and data
            _logger.LogInformation("Verifying webhook for order code {OrderCode}", webhookData.Data.OrderCode);
            PayOS.Models.Webhooks.WebhookData data;
            try
            {
                data = await _payOSClient.Webhooks.VerifyAsync(webhook);
            }
            catch (PayOSException ex)
            {
              
                _logger.LogWarning(
                    ex,
                    "PayOS webhook verification failed ");
                return false;
            }

            // Validate webhook verification result
            if (data == null)
            {
                _logger.LogWarning("Webhook verification returned null for order code {OrderCode}", webhookData.Data.OrderCode);
                return false;
            }

            if (!webhookData.Success)
            {
                _logger.LogWarning(
                    "Verified PayOS webhook has success flag false for order code {OrderCode}. Continuing with payment code {PaymentCode}",
                    webhookData.Data.OrderCode,
                    data.Code);
            }

            //check if payment with order code exists
            var existingPayment = await _paymentRepository.FindAsync(x => x.OrderCode == webhookData.Data.OrderCode);
            if (existingPayment == null)
            {
                _logger.LogWarning("Verified PayOS webhook for unknown order code {OrderCode}", webhookData.Data.OrderCode);
                return true;
            }
            if (existingPayment.Status == PaymentEnums.Completed)
            {
                _logger.LogWarning("Payment with order code {OrderCode} has already been completed", webhookData.Data.OrderCode);
                return true;
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

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                switch (data.Code)
                {
                    case "00":
                        // Only create subscription and quota for successful payments
                        var now = DateTimeOffset.UtcNow;
                        var canApplySubscription = await CloseCurrentSubscriptionForUpgradeIfNeededAsync(
                            existingPayment.BussinessId,
                            subscriptionPlan,
                            now);
                        if (!canApplySubscription)
                        {
                            await _unitOfWork.RollBackAsync();
                            return false;
                        }

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
                            UsedTokens = 0,
                            MaxProductAllowed = subscriptionPlan.MaxProductAllowed
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
                if (existingPayment.Status == PaymentEnums.Completed)
                {
                    await _publishEndpoint.Publish(new PaymentCompletedEvent
                    {
                        PaymentId = existingPayment.Id.ToString()
                    });
                }
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

            if (existingPayment.Status != PaymentEnums.Pending)
            {
                _logger.LogWarning("Payment with order code {OrderCode} is not in pending status", orderCode);
                return Result<Payment>.Failure(400, "Payment is not in pending status");
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var now = DateTimeOffset.UtcNow;
                var canApplySubscription = await CloseCurrentSubscriptionForUpgradeIfNeededAsync(
                    existingPayment.BussinessId,
                    subscriptionPlan,
                    now);
                if (!canApplySubscription)
                {
                    await _unitOfWork.RollBackAsync();
                    return Result<Payment>.Failure(400, "Business already has an active subscription for this subscription plan");
                }

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
                    UsedTokens = 0,
                    MaxProductAllowed = subscriptionPlan.MaxProductAllowed
                };

                existingPayment.Status = PaymentEnums.Completed;
                await _subscriptionRepository.AddAsync(subscription);
                await _businessQuotaRepository.AddAsync(businessQuota);
                await _paymentRepository.UpdateAsync(existingPayment);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                return Result<Payment>.Success(existingPayment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing payment successful");
                await _unitOfWork.RollBackAsync();
                return Result<Payment>.Failure(500, "An error occurred while testing payment successful");
            }
        }

        //check update if level of selected plan is higher than current plan
        private async Task<bool> CloseCurrentSubscriptionForUpgradeIfNeededAsync(
            ObjectId businessId,
            SubscriptionPlan selectedPlan,
            DateTimeOffset now)
        {
            var currentSubscription = await _subscriptionRepository
                .FindAsync(x => x.BusinessId == businessId && x.StartDate <= now && x.EndDate > now && x.Status == StatusEnums.Active);
            if (currentSubscription == null)
            {
                return true;
            }

            var currentPlan = await _subscriptionPlanRepository.FindAsync(x => x.Id == currentSubscription.SubscriptionPlanId);
            if (currentPlan == null)
            {
                _logger.LogWarning("Current subscription plan with id {SubscriptionPlanId} not found", currentSubscription.SubscriptionPlanId);
                return false;
            }

            if (selectedPlan.Level <= currentPlan.Level)
            {
                _logger.LogWarning(
                    "Business with id {BusinessId} cannot change from plan level {CurrentLevel} to plan level {SelectedLevel}",
                    businessId,
                    currentPlan.Level,
                    selectedPlan.Level);
                return false;
            }

            currentSubscription.Status = StatusEnums.Inactive;
            currentSubscription.EndDate = now;
            await _subscriptionRepository.UpdateAsync(currentSubscription);
            return true;
        }

        private static long GenerateOrderCode()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var randomPart = Random.Shared.Next(100, 999);

            return checked(timestamp * 1000 + randomPart);
        } 
        
    }
}
