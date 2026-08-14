using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.AIUsageDashboard
{
    public class AIUsageDashboardQuery : IRequest<Result<AIUsageDashboardResponse>>
    {
        public AIUsageDashboardFilter Filter { get; set; } = new AIUsageDashboardFilter();
    }
}
