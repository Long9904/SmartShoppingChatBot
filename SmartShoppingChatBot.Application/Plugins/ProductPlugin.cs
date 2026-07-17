using System.ComponentModel;
using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch;

namespace SmartShoppingChatBot.Application.Plugins
{
    public class ProductPlugin
    {
        private readonly IMediator _mediator;

        public ProductPlugin(IMediator mediator)
        {
            _mediator = mediator;
        }

        [KernelFunction]
        [Description("Tìm sản phẩm theo ngữ nghĩa kết hợp bộ lọc có cấu trúc. Must response with JSON")]
        public async Task<object> SemanticProductSearch(
            [Description("Bộ lọc cấu trúc")] ProductSemanticSearchRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ProductSemanticSearchQuery { Request = request },
                cancellationToken);

            return result;
        }
    }
}
