using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class CreatePaymentRequest
    {
        public string SubscriptionPlantId { get; set; } = string.Empty;
        public string BussinessId { get; set; } = string.Empty;
        public string? ReturnUrlDomain { get; set; }

    }

    public class PaymentResponsed
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public long OrderCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    public class PayOSWebhookRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
        [JsonPropertyName("desc")]
        public string Desc { get; set; } = string.Empty;
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("data")]
        public PayOSWebhookData Data { get; set; } = new PayOSWebhookData();
        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public class PayOSWebhookData
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("transactionDateTime")]
        public string TransactionDateTime { get; set; } = string.Empty;
    }
}
