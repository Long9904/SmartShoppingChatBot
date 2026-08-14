namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ProductResponseV3
{
    [System.Text.Json.Serialization.JsonPropertyName("productId")]
    [System.ComponentModel.Description("Canonical product ID. Copy this exact value when referencing the product.")]
    public string ProductId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("externalProductId")]
    public string ExternalProductId { get; set; } = string.Empty;

    public string ExternalProductUrl { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Price { get; set; }

    public string? Brand { get; set; }

    public int StockQuantity { get; set; }

    public string Category { get; set; } = string.Empty;

    public double Score { get; set; }

    public static ProductResponseV3 FromProduct(ProductResponseV2 product, double score = 0)
    {
        return new ProductResponseV3
        {
            ProductId = product.ProductId,
            ExternalProductId = product.ExternalProductId,
            ExternalProductUrl = product.ExternalProductUrl,
            Name = product.Name,
            Price = product.Price,
            Brand = product.Brand,
            StockQuantity = product.StockQuantity,
            Category = product.Category,
            Score = score
        };
    }

    public ProductResponseV3 Copy()
    {
        return new ProductResponseV3
        {
            ProductId = ProductId,
            ExternalProductId = ExternalProductId,
            ExternalProductUrl = ExternalProductUrl,
            Name = Name,
            Price = Price,
            Brand = Brand,
            StockQuantity = StockQuantity,
            Category = Category,
            Score = Score
        };
    }

    public ProductResponseV2 ToProductResponseV2()
    {
        return new ProductResponseV2
        {
            ProductId = ProductId,
            ExternalProductId = ExternalProductId,
            ExternalProductUrl = ExternalProductUrl,
            Name = Name,
            Price = Price,
            Brand = Brand,
            StockQuantity = StockQuantity,
            Category = Category
        };
    }
}
