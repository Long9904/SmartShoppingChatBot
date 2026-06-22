using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.Features.UpdateSubscription
{
    public record SubscriptionUpdateCommand : IRequest<Result<SubscriptionResponse>>
    {
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }

    }
}
