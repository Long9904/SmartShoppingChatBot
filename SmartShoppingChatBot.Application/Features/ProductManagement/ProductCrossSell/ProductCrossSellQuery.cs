using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCrossSell;

public sealed class ProductCrossSellQuery : IRequest<Result<List<ProductResponseV3>>>
{
    public ProductCrossSellRequest Request { get; init; } = default!;
}
