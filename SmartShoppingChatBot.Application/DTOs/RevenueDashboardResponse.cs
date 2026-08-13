using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class RevenueDashboardResponse
    {
        public int TotalRevenue { get; set; }
        public int TotalRevenueThisMonth { get; set; }
        public int ActiveSubscriptionCount { get; set; }
        public int TotalSubscriptionCount { get; set; }
        public int CancelledSubscriptionCount { get; set; }

    }
}
