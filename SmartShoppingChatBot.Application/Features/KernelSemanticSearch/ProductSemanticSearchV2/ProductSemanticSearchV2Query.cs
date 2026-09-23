using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;

public sealed class ProductSemanticSearchV2Query : IRequest<Result<List<ProductReferenceV2>>>
{
    public ProductSemanticSearchV2Request Request { get; init; } = new();
    // Internal recovery flag, not an AI-controlled search filter.
    public bool IncludeBm25Candidates { get; init; }
}
