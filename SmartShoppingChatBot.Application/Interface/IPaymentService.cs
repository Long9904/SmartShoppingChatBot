using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IPaymentService
    {
        Task<Result<PaymentResponsed>> CreatePaymentLink(CreatePaymentRequest request);
        Task<bool> VerifyPaymentWebhook(PayOSWebhookRequest webhookData);  
        Task<Result<Payment>> TestPaymentSuccessfull(long orderCode);
        
    }
}
