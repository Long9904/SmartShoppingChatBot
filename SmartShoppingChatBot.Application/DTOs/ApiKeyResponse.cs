using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ApiKeyResponse
    {
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string KeyId { get; set; } = string.Empty;

        public string? MaskedKey { get; set; }

        public KeyStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
