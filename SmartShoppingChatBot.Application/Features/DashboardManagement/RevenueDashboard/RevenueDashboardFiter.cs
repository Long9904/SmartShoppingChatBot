using SmartShoppingChatBot.Application.Commons.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.RevenueDashboard
{
    public class RevenueDashboardFiter : QueryBase
    {
        public int Month { get; set; } = DateTime.Now.Month;
    }
}
