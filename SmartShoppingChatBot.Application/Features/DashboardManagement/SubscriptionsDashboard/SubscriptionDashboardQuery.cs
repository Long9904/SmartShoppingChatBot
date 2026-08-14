using MediatR;
using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.SubscriptionsDashboard
{
    public class SubscriptionDashboardQuery : IRequest<Result<BasePaginatedList<SubscriptionDashboardResponse>>>
    {
        public SubscriptionDashboardFilter Filter { get; set; } = new SubscriptionDashboardFilter();
    }
}
