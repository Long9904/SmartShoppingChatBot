using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class CreateApiKeyResponse
    {
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string KeyId { get; set; } = string.Empty;

        public string? FullKey { get; set; }

        public KeyStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
