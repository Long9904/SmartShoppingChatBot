using MediatR;
using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations
{
    public class CustomerGetConversationsQuery : QueryBase, IRequest<Result<BasePaginatedList<CustomerConversationResponse>>>
    {
        public string ExternalCustomerId { get; set; } = string.Empty;
    }
}
