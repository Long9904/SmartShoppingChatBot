using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.GetAllSubscription
{
    public class GetSubscriptionFilter : QueryBase
    {
        public string? Search { get; set; }

        public StatusEnums? Status { get; set; }
    }
}
