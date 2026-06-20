using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.GetAllSubscription
{
    public class GetSubscriptionQuery : IRequest<Result<BasePaginatedList<SubscriptionResponse>>>
    {
        public GetSubscriptionFilter? Filter { get; set; }
    }
}
