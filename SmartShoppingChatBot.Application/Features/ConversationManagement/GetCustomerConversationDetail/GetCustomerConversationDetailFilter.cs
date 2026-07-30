using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;

public sealed class GetCustomerConversationDetailFilter
{
    public string? LastCursor { get; set; }

    public int Limit { get; set; } = 20;

    public string? Search { get; set; }

    public SenderTypeEnum? SenderType { get; set; }
}
