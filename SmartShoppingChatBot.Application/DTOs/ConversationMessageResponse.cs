using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ConversationMessageResponse
    {
        public string? Id { get; set; }

        public required string Content { get; set; }

        public SenderTypeEnum SenderType { get; set; }

        public ContentTypeEnum ContentType { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<MessageProductResponse>? ProductReferences { get; set; }
    }
}
