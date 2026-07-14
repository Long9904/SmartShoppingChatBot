using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.DeleteSubscription
{
    public class DeleteSubscriptionCommand : IRequest<Result<string>>
    {
        public string Id { get; init; } = string.Empty;
    }
}
