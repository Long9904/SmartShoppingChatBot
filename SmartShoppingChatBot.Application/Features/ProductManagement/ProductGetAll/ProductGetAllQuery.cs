using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;

public class ProductGetAllQuery : IRequest<Result<BasePaginatedList<ProductResponse>>>
{
    public ProductGetAllFilter Filter { get; set; } = new();
}
