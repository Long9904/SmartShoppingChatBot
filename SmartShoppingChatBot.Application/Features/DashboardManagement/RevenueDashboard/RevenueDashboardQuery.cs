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

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.RevenueDashboard
{
    public class RevenueDashboardQuery : IRequest<Result<BasePaginatedList<RevenueDashboardResponse>>>
    {
        public RevenueDashboardFiter Filter { get; set; } = new RevenueDashboardFiter();
    }
}
