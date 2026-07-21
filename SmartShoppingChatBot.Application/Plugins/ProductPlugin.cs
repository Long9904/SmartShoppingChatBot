using System.ComponentModel;
using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Plugins
{
    public class ProductPlugin
    {
        private readonly IMediator _mediator;
        private readonly IProductReferenceCollector _productReferenceCollector;

        public ProductPlugin(IMediator mediator, IProductReferenceCollector productReferenceCollector)
        {
            _mediator = mediator;
            _productReferenceCollector = productReferenceCollector;
        }

        [KernelFunction]
        [Description("Tìm sản phẩm theo ngữ nghĩa kết hợp bộ lọc có cấu trúc. Must response with JSON")]
        public async Task<object> SemanticProductSearch(
            [Description("Bộ lọc cấu trúc")] ProductSemanticSearchRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ProductSemanticSearchQuery { Request = request }, cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }
    }
}
