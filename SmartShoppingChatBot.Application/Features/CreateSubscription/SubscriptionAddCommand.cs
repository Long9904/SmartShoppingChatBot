using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.SubscriptionAdd
{
    public record SubscriptionAddCommand : IRequest<Result<SubscriptionResponse>> 
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }

    }
}
