using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities;

public class Product
{
    [Key]
    public ObjectId Id { get; set; } = default!;

    public ObjectId BusinessId { get; set; } = default!;

    public string ExternalId { get; set; } = default!;

    public string ExternalProductUrl { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "VND";

    public string? Brand { get; set; }

    public int StockQuantity { get; set; }

    public string Category { get; set; } = default!; // e.g., laptop > MSI or phone > Samsung

    [BsonRepresentation(BsonType.String)]
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public List<string> Images { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];

    // Qdrant sync
    public Guid QdrantPointId { get; set; }

    public DateTimeOffset EmbbbedAt { get; set; }

    // Log
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Import Job
    public ObjectId? ImportJobId { get; set; }

    public UserEmbedded CreatedBy { get; set; } = default!;

    public UserEmbedded UpdatedBy { get; set; } = default!;


    public void SetData(string key, string value)
    {
        Metadata[key] = value;
    }

    public T? GetMeta<T>(string key)
    {
        if (!Metadata.ContainsKey(key))
            return default;

        return BsonSerializer.Deserialize<T>(Metadata[key].ToBsonDocument());
    }

    public string BuildEmbeddingText()
    {
        var embeddingText = new List<string> { Name };

        if (!string.IsNullOrEmpty(Description))
            embeddingText.Add("Mô tả sản phẩm: " + Description);

        if (!string.IsNullOrEmpty(Brand))
            embeddingText.Add("Thương hiệu: " + Brand);

        embeddingText.Add("Danh mục: " + Category);
        embeddingText.Add("Giá: " + Price + " " + Currency);

        foreach (var data in Metadata)
            embeddingText.Add(data.Key + ": " + data.Value);

        return string.Join("\n", embeddingText);
    }
}


