using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;

public static class ProductMappings
{
    public static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id.ToString(),
            BusinessId = product.BusinessId.ToString(),
            ExternalId = product.ExternalId,
            ExternalProductUrl = product.ExternalProductUrl,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Currency = product.Currency,
            Brand = product.Brand,
            StockQuantity = product.StockQuantity,
            Category = product.Category,
            Status = product.Status,
            Images = product.Images,
            Metadata = product.Metadata,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public static Dictionary<string, Value> BuildQdrantPayload(Product product)
    {
        var payload = new Dictionary<string, Value>
        {
            [ProductPayloadNames.ProductId] = product.Id.ToString(),
            [ProductPayloadNames.BusinessId] = product.BusinessId.ToString(),
            [ProductPayloadNames.Price] = (double)product.Price,
            [ProductPayloadNames.Status] = product.Status.ToString(),
            [ProductPayloadNames.ExternalId] = product.ExternalId,
            [ProductPayloadNames.Name] = product.Name,
            [ProductPayloadNames.Category] = product.Category
        };

        return payload;
    }
}
