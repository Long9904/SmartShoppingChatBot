using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch
{
    public class ProductSemanticSearchQuery : IRequest<Result<List<ProductResponse>>>
    {
        public ProductSemanticSearchRequest Request { get; init; } = new();
    }
}
