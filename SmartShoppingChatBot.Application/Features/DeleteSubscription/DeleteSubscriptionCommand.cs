using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DeleteSubscription
{
    public class DeleteSubscriptionCommand : IRequest<Result<string>>
    {
        public string Id { get; init; } = string.Empty;
    }
}
