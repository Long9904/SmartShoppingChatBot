using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.UpdateSubscription
{
    public record SubscriptionUpdateCommand : IRequest<Result<SubscriptionResponse>> 
    {
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public int Level { get; set; }
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }
        public int MaxProductAllowed { get; set; }
        public int MaxDocumentAllowed { get; set; }
    }
}
