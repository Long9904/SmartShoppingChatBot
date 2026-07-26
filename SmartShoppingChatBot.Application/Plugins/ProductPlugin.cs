using System.ComponentModel;
using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Commons.Results;
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
        [Description("Use whenever the user asks to find, browse, show, recommend, buy, or get information about products. No COMPARE")]
        [return: Description("Product search result. Every product has a canonical productId that must be copied exactly when the product is referenced.")]
        public async Task<Result<List<ProductResponseV2>>> SemanticProductSearch(
            [Description("Bộ lọc cấu trúc")] ProductSemanticSearchRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ProductSemanticSearchQuery { Request = request }, cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }
    }
}
