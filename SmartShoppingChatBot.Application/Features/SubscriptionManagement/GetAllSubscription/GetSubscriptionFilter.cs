using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.GetAllSubscription
{
    public class GetSubscriptionFilter : QueryBase
    {
        public string? Search { get; set; }

        public StatusEnums? Status { get; set; }
    }
}
