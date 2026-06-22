using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IPaymentService
    {
        Task<Result<PaymentResponsed>> CreatePaymentLink(CreatePaymentRequest request);
        Task<bool> VerifyPaymentWebhook(PayOSWebhookRequest webhookData);
        Task<Result<Payment>> TestPaymentSuccessful(long orderCode);

    }
}
