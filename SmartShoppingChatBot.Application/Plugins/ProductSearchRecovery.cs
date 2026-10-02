using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;

namespace SmartShoppingChatBot.Application.Plugins;

public sealed class ProductSearchRecovery
{
    public ProductSearchReview Review { get; init; } = new();
    public bool SearchWasCalled { get; init; }
    public List<ProductReferenceV2> Candidates { get; init; } = [];
    public List<ProductSearchRecoveryStep> Steps { get; init; } = [];
}

public sealed class ProductSearchRecoveryStep
{
    public string Function { get; init; } = string.Empty;
    public ProductSemanticSearchV2Request? SearchRequest { get; init; }
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<ProductReferenceV2> Products { get; init; } = [];
}
