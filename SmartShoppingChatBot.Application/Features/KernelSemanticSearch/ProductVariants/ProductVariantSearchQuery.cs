using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductVariants;

public sealed record ProductVariantSearchQuery(
    ProductResponseV2 Source,
    ProductVariantSearchRequest Request) : IRequest<Result<List<ProductReferenceV2>>>;
