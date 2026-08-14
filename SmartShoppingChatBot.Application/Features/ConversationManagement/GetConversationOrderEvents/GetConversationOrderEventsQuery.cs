using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationOrderEvents;

public sealed class GetConversationOrderEventsQuery
    : IRequest<Result<CursorPage<ConversationOrderEventResponse>>>
{
    public string CustomerExternalId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public GetConversationOrderEventsFilter Filter { get; init; } = new();
}
