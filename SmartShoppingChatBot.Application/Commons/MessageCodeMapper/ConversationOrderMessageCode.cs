namespace SmartShoppingChatBot.Application.Commons.MessageCodeMapper;

public static class ConversationOrderMessageCode
{
    public const string Success = "MG_CONVERSATION_ORDER_200";
    public const string NotFound = "MG_CONVERSATION_ORDER_404";
    public const string AlreadyExists = "MG_CONVERSATION_ORDER_409";
    public const string InvalidStatus = "MG_CONVERSATION_ORDER_STATUS_400";
    public const string ServerError = "MG_CONVERSATION_ORDER_500";
}
