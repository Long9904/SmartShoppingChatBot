using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs;

public enum ProductPriceBandV3
{
    Any,
    Low,
    Medium,
    High
}

public enum ProductPriceStrategyV3
{
    UpSell,
    DownSell
}

public enum ProductSortV3
{
    Relevance,
    PriceLowToHigh,
    PriceHighToLow
}

public sealed class ProductAttributeFilterV3
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

public sealed class ProductCategoryBrowseRequestV3
{
    [Description("Exact active category from the supplied category list.")]
    public required string Category { get; init; }

    [Description("Any, Low for cheap/budget/binh dan, Medium for mid-range, High for expensive/premium. The server reads price limits from business config.")]
    public ProductPriceBandV3 PriceBand { get; init; }

    [Description("Explicit numeric budget only. Do not invent limits for cheap or expensive.")]
    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    [Description("Canonical product IDs already shown when the customer asks for other products.")]
    public List<string> ExcludeProductIds { get; init; } = [];
}

public sealed class ProductSearchRequestV3
{
    [Description("Describe the target product and current needs. Keep style, occasion and explicit requirements; omit price wording.")]
    public required string SemanticQuery { get; init; }
    [Description("Short catalogue query describing the target product. Omit price wording.")]
    public required string TechnicalQuery { get; init; }
    [Description("Exact active category from the supplied list, or null when no category is specified.")]
    public string? Category { get; init; }
    [Description("Any, Low for cheap/budget/binh dan, Medium for mid-range, High for expensive/premium. The server reads price limits from business config.")]
    public ProductPriceBandV3 PriceBand { get; init; }
    [Description("Explicit numeric budget only. Do not invent limits for cheap or expensive.")]
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    [Description("Only explicit requirements with keys/values in the selected category schema. Never turn a styling suggestion into a hard filter.")]
    public List<ProductAttributeFilterV3> Attributes { get; init; } = [];

    [Description("Use Relevance for normal search. Use PriceLowToHigh or PriceHighToLow only when the customer explicitly asks for the cheapest or most expensive matching products.")]
    public ProductSortV3 Sort { get; init; } = ProductSortV3.Relevance;

    public List<string> ExcludeProductIds { get; init; } = [];
}

public sealed class ProductCrossSellRequestV3
{
    public required string ReferenceProductId { get; init; }
    [Description("One to three searches for complementary products, each with an exact target category. Different product types may share a broad category.")]
    public List<ProductSearchRequestV3> Targets { get; init; } = [];
}

public sealed class ProductPriceAlternativeRequestV3
{
    public required string ReferenceProductId { get; init; }
    public ProductPriceStrategyV3 Strategy { get; init; }
    [Description("Replacement product requirements. Keep the original type and user constraints. Never compute the reference price.")]
    public required ProductSearchRequestV3 Search { get; init; }
}

public sealed class ProductByIdsRequestV3
{
    public List<string> ProductIds { get; init; } = [];
}

public sealed class ProductCrossSellGroupV3
{
    public string Category { get; init; } = string.Empty;
    public string SearchNeed { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public List<ProductReferenceV3> Products { get; init; } = [];
}
