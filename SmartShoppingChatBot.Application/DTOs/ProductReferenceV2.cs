using System.Text.Json.Serialization;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

public class ProductReferenceV2
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
    public decimal? Price { get; set; }
    public string? Brand { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public List<string> Images { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public Dictionary<string, string> QdrantPayload { get; set; } = [];
    public double Score { get; set; }


    public static ProductReferenceV2 FromProduct(Product product) => new()
    {
        ProductId = product.Id.ToString(),
        ExternalProductId = product.ExternalId,
        HasCurrentData = true,
        Name = product.Name,
        Description = product.Description,
        ExternalProductUrl = product.ExternalProductUrl,
        Price = product.Price,
        Brand = product.Brand,
        Category = product.Category,
        StockQuantity = product.StockQuantity,
        Images = product.Images.ToList(),
        Metadata = new Dictionary<string, string>(product.Metadata)
    };

    public ProductReferenceV2 Copy()
    {
        var copy = (ProductReferenceV2)MemberwiseClone();
        copy.Images = Images.ToList();
        copy.Metadata = new Dictionary<string, string>(Metadata);
        copy.QdrantPayload = new Dictionary<string, string>(QdrantPayload);
        return copy;
    }
}
