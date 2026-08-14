using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationSearchQueryLogs;

public sealed class GetConversationSearchQueryLogsQuery
    : IRequest<Result<CursorPage<SearchQueryLogResponse>>>
{
    public string CustomerExternalId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public GetConversationSearchQueryLogsFilter Filter { get; init; } = new();
}
