using System.Text.Json.Serialization;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductUpdate;

public class ProductUpdateCommand : IRequest<Result<ProductResponse>>
{
    [JsonIgnore]
    public ObjectId? ProductId { get; set; }

    [JsonIgnore]
    public string? LookupExternalId { get; set; }

    public string ExternalId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public string? ExternalProductUrl { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "VND";

    public string? Brand { get; set; }

    public int StockQuantity { get; set; }

    public string Category { get; set; } = default!;

    public List<string> Images { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];
}
