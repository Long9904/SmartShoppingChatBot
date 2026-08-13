using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.SummaryDashboard
{
    public class SummaryDashboardQuery : IRequest<Result<SummaryResponse>>
    {
        public SummaryDashboardFilter Filter { get; set; } = new SummaryDashboardFilter();
    }
}
