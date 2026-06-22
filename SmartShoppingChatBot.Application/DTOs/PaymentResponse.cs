using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class PaymentResponse
    {
        public string Id { get; set; } = string.Empty;
        public BusinessResponseV1 Bussiness { get; set; } = new BusinessResponseV1();
        public PlanResponse SubscriptionPlan { get; set; } = new PlanResponse();
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public PaymentEnums Status { get; set; } = PaymentEnums.Pending;
        public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTimeOffset? UpdatedAt { get; set; }
    }
    public class BusinessResponseV1
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
    public class PlanResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
