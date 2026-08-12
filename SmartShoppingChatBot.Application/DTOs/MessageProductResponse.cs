namespace SmartShoppingChatBot.Application.DTOs;

public sealed class MessageProductResponse
{
    public string ProductId { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string ExternalProductUrl { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Price { get; set; }

    public int StockQuantity { get; set; }

    public static MessageProductResponse FromProduct(ProductResponseV2 product)
    {
        return new MessageProductResponse
        {
            ProductId = product.ProductId,
            ExternalId = product.ExternalProductId,
            ExternalProductUrl = product.ExternalProductUrl,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        };
    }
}
