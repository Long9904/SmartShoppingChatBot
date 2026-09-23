using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

public sealed class CategorySemanticSearchQuery : IRequest<Result<List<ProductReferenceV2>>>
{
    public CategorySemanticSearchRequest Request { get; init; } = new();
}
