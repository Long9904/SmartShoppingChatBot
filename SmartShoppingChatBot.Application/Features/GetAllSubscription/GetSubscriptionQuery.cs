using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.GetAllSubscription
{
    public class GetSubscriptionQuery : IRequest<Result<BasePaginatedList<SubscriptionResponse>>>
    {
        public GetSubscriptionFilter? Filter { get; set; }
    }
}
