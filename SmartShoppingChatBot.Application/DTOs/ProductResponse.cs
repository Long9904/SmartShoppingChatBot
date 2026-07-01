using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductResponse
    {
        public string Id { get; set; } = string.Empty;

        public string BusinessId { get; set; } = string.Empty;

        public string ExternalId { get; set; } = string.Empty;

        public string Name { get; set; } = default!;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "VND";

        public string? Brand { get; set; }

        public int StockQuantity { get; set; }

        public string Category { get; set; } = default!; // e.g., laptop > MSI or phone > Samsung

        public ProductStatus Status { get; set; } = ProductStatus.PendingEmbedding;

        public List<string> Images { get; set; } = [];

        public Dictionary<string, string> Metadata { get; set; } = [];

        public DateTimeOffset CreatedAt { get; set; }

    }
}
