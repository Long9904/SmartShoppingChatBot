using SmartShoppingChatBot.Application.Commons.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.AIUsageDashboard
{
    public class AIUsageDashboardFilter : QueryBase
    {
        public int Range { get; set; }
    }
}
