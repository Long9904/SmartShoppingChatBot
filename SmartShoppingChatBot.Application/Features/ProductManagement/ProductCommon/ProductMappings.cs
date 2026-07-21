using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;

internal static class ProductMappings
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
            ["mongo_id"] = product.Id.ToString(),
            ["business_id"] = product.BusinessId.ToString(),
            [ProductPayloadNames.ExternalId] = product.ExternalId,
            ["name"] = product.Name,
            ["description"] = product.Description ?? string.Empty,
            [ProductPayloadNames.ExternalUrl] = product.ExternalProductUrl,
            ["price"] = (double)product.Price,
            [ProductPayloadNames.Currency] = product.Currency,
            ["brand"] = product.Brand ?? string.Empty,
            [ProductPayloadNames.StockQuantity] = (long)product.StockQuantity,
            ["category"] = product.Category,
            ["status"] = product.Status.ToString(),
            [ProductPayloadNames.Images] = product.Images.ToArray()
        };

        foreach (var metadata in product.Metadata)
        {
            if (!payload.ContainsKey(metadata.Key))
            {
                payload[metadata.Key] = metadata.Value;
            }
        }

        return payload;
    }
}
