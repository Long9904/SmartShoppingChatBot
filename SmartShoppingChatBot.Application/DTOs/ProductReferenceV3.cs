using System.Text.Json.Serialization;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

// Tools, the collector, and conversation turns share this product snapshot.
public sealed class ProductReferenceV3
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;
    [JsonPropertyName("externalProductId")]
    public string? ExternalProductId { get; set; }
    public int DisplayOrder { get; set; }
    public bool HasCurrentData { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ExternalProductUrl { get; set; } = string.Empty;
    public string? Price { get; set; }
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public List<string> Images { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public double Score { get; set; }

    public static ProductReferenceV3 FromProduct(Product product) => new()
    {
        ProductId = product.Id.ToString(), ExternalProductId = product.ExternalId, HasCurrentData = true,
        Name = product.Name, Description = product.Description,
        ExternalProductUrl = product.ExternalProductUrl,
        Price = product.Price + " " + product.Currency,
        Brand = product.Brand, Category = product.Category,
        StockQuantity = product.StockQuantity, Images = product.Images.ToList(),
        Metadata = new Dictionary<string, string>(product.Metadata)
    };

    public ProductReferenceV3 Copy()
    {
        var copy = (ProductReferenceV3)MemberwiseClone();
        copy.Images = Images.ToList();
        copy.Metadata = new Dictionary<string, string>(Metadata);
        return copy;
    }

    // Keep the public response compatible with existing clients.
    public MessageProductResponse ToMessageResponse() => new()
    {
        ProductId = ProductId, ExternalId = ExternalProductId ?? string.Empty,
        ExternalProductUrl = ExternalProductUrl, Name = Name,
        Price = Price, StockQuantity = StockQuantity
    };
}
