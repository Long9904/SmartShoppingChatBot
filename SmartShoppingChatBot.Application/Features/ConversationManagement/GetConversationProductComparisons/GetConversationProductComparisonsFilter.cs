namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationProductComparisons;

public sealed class GetConversationProductComparisonsFilter
{
    public string? LastCursor { get; init; }

    public int Limit { get; init; } = 20;
}
