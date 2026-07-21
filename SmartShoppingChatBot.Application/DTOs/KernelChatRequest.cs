using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class KernelChatRequest
    {
        public required string UserMessage { get; set; }

        public required Business Business { get; set; }

        public ConversationContextCache? ConversationContextCache { get; set; }
    }
}
