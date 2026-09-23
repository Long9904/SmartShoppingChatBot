namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductResponseV2
    {
        [System.Text.Json.Serialization.JsonPropertyName("productId")]
        [System.ComponentModel.Description("Canonical product ID. Copy this exact value when referencing the product.")]
        public string ProductId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("externalProductId")]
        public string ExternalProductId { get; set; } = string.Empty;

        public string ExternalProductUrl { get; set; } = string.Empty;

        public string Name { get; set; } = default!;

        public string? Description { get; set; }

        public string? Price { get; set; }

        public string? Brand { get; set; }

        public int StockQuantity { get; set; }

        public string Category { get; set; } = default!; // e.g., laptop > MSI or phone > Samsung

        public List<string> Images { get; set; } = [];

        public Dictionary<string, string> Metadata { get; set; } = [];

        [System.ComponentModel.Description("Các field scalar của Qdrant payload, gồm category và các thuộc tính canonical dùng để lọc. Khác với Category/Metadata nguồn từ MongoDB.")]
        public Dictionary<string, string> QdrantPayload { get; set; } = [];

        public double Score { get; set; }

        public ProductResponseV2 Copy()
        {
            return new ProductResponseV2
            {
                ProductId = ProductId,
                ExternalProductId = ExternalProductId,
                ExternalProductUrl = ExternalProductUrl,
                Name = Name,
                Description = Description,
                Price = Price,
                Brand = Brand,
                StockQuantity = StockQuantity,
                Category = Category,
                Images = Images.ToList(),
                Metadata = new Dictionary<string, string>(Metadata),
                QdrantPayload = new Dictionary<string, string>(QdrantPayload),
                Score = Score
            };
        }
    }
}
