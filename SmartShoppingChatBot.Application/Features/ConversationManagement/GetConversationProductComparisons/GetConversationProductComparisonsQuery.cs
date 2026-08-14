using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationProductComparisons;

public sealed class GetConversationProductComparisonsQuery
    : IRequest<Result<CursorPage<ProductComparisonResponse>>>
{
    public string CustomerExternalId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public GetConversationProductComparisonsFilter Filter { get; init; } = new();
}
