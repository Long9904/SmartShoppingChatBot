using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.SubscriptionsDashboard
{
    public class SubscriptionDashboardFilter : QueryBase
    {
        public StatusEnums Status { get; set; }

    }
}
