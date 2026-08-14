namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationOrderEvents;

public sealed class GetConversationOrderEventsFilter
{
    public string? LastCursor { get; init; }

    public int Limit { get; init; } = 20;
}
