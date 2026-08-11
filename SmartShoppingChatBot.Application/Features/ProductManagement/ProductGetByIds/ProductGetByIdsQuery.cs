using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;

public sealed class ProductGetByIdsQuery : IRequest<Result<List<ProductResponseV2>>>
{
    public required List<string> ProductIds { get; init; }
}
