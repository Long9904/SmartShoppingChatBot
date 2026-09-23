using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Plugins;

public sealed class ProductSearchRecovery
{
    public ProductSearchReview Review { get; init; } = new();
    public bool SearchWasCalled { get; init; }
    public List<ProductSearchRecoveryStep> Steps { get; init; } = [];
}

public sealed class ProductSearchRecoveryStep
{
    public string Function { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<ProductReferenceV2> Products { get; init; } = [];
}
