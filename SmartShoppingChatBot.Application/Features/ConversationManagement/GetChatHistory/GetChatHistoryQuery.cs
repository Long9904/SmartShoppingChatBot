using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory
{
    public class GetChatHistoryQuery : IRequest<Result<CursorPage<ConversationMessageResponse>>>
    {
        public string ConversationId { get; set; } = string.Empty;

        public string ExternalCustomerId { get; set; } = string.Empty;

        public string? LastCursor { get; set; }

        public int Limit { get; set; } = 20;
    }
}
