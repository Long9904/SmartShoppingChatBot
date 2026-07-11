using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.CreateSubscription
{
    public record SubscriptionAddCommand : IRequest<Result<SubscriptionResponse>>
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }
        public int MaxProductAllowed { get; set; }
        public int MaxDocmentAllowed { get; set; }

    }
}
