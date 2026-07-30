using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;

public sealed class GetCustomerConversationDetailQuery
    : IRequest<Result<CursorPage<ConversationMessageResponse>>>
{
    public string CustomerExternalId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public GetCustomerConversationDetailFilter Filter { get; init; } = new();
}
