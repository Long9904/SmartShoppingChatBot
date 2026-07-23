namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductResponseV2
    {
        public string Id { get; set; } = string.Empty;

        public string ExternalProductUrl { get; set; } = string.Empty;

        public string Name { get; set; } = default!;

        public string? Description { get; set; }

        public string? Price { get; set; }

        public string? Brand { get; set; }

        public int StockQuantity { get; set; }

        public string Category { get; set; } = default!; // e.g., laptop > MSI or phone > Samsung

        public List<string> Images { get; set; } = [];

        public Dictionary<string, string> Metadata { get; set; } = [];
    }
}
