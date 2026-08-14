using MassTransit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class SubscriptionDashboardResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StatusEnums Status { get; set; } = StatusEnums.Active;
        public DetailResponse Detail { get; set; } = new DetailResponse();
    }

    public class DetailResponse
    {
        public double Rate { get; set; }
        public int BusinessCount { get; set; }

    }

    public class SummaryResponse
    {
        public int TotalBusiness { get; set; }
        public int ActiveBusiness { get; set; }
        public int TotalUsers { get; set; }
        public int TotalProduct { get; set; }
        public int TotalDocument { get; set; }
        public int TotalChatSession { get; set; }
        public int TotalMessage { get; set; }
        public int TotalTokenUsed { get; set; }
        public int TotalRevenue { get; set; }
        public int ActiveSubscriptionCount { get; set; }
    }

    public class AIUsageDashboardResponse
    {
        public int TotalTokenUsed { get; set; }
        public int InputTokenUsed { get; set; }
        public int OutputTokenUsed { get; set; }
        public int TotalMessageUsed { get; set; }
        public List<AIUsageDashboardChartResponse> ChartData { get; set; } = new List<AIUsageDashboardChartResponse>();
    }
    public class AIUsageDashboardChartResponse
    {
        public DateOnly Date { get; set; }
        public int TotalTokenUsed { get; set; }
    }
}
