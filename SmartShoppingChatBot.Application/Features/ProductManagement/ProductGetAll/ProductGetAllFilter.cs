using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;

public class ProductGetAllFilter : QueryBase
{
    public string? ExternalId { get; set; }

    public string? Name { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public int? MinStockQuantity { get; set; }

    public int? MaxStockQuantity { get; set; }

    public string? Category { get; set; }

    public ProductStatus? Status { get; set; }

    public string? OrderBy { get; set; } = "UpdatedAt desc";
}
