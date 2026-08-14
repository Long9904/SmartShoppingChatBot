namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationSearchQueryLogs;

public sealed class GetConversationSearchQueryLogsFilter
{
    public string? LastCursor { get; init; }

    public int Limit { get; init; } = 20;
}
