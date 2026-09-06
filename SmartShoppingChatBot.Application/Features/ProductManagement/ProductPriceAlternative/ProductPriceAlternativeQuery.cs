using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductPriceAlternative;

public sealed class ProductPriceAlternativeQuery : IRequest<Result<List<ProductResponseV2>>>
{
    public ProductPriceAlternativeRequest Request { get; init; } = default!;
}
