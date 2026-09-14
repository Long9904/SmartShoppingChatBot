namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ResolvedProductReference
{
    public string ProductId { get; init; } = string.Empty;

    public string ExternalProductId { get; init; } = string.Empty;

    public string ExternalProductUrl { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Price { get; init; }

    public int StockQuantity { get; init; }

    public string Category { get; init; } = string.Empty;

    public static ResolvedProductReference FromProduct(ProductResponseV2 product)
    {
        return new ResolvedProductReference
        {
            ProductId = product.ProductId,
            ExternalProductId = product.ExternalProductId,
            ExternalProductUrl = product.ExternalProductUrl,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            Category = product.Category
        };
    }
}
