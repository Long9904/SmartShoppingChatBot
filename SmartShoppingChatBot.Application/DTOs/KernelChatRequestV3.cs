using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class KernelChatRequestV3
{
    public required string UserMessage { get; set; }
    public required Business Business { get; set; }
    public ConversationContextCacheV3? ConversationContextCache { get; set; }
}
