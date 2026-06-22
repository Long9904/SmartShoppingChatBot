using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.GetAllPayment
{
    public class GetPaymentFilter : QueryBase
    {
        public string? Search { get; set; }
        public PaymentEnums? PaymentEnums { get; set; }
        public string? CreateAtOrderBy { get; set; }
    }
}
