using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;

public sealed class ProductGetByExternalIdsQuery : IRequest<Result<List<ProductReferenceV2>>>
{
    public ProductGetByExternalIdsRequest Request { get; init; } = new();
}
