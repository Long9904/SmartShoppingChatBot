using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPaymentByUser
{
    public class GetPaymentFilterByUser : QueryBase
    {
        public string? Search { get; set; }
        public PaymentEnums? PaymentEnums { get; set; }
        public string? CreateAtOrderBy { get; set; }
    }
}
