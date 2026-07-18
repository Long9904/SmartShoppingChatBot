using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class CustomerConversationResponse
    {
        public string? Id { get; set; }

        public required string Title { get; set; }

        public ConversationStatus Status { get; set; }

        public DateTimeOffset LastMessageAt { get; set; }

        public DateTimeOffset CreateAt { get; set; }
    }
}
