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
        [Description("Tìm sản phẩm khi người dùng muốn xem, tìm, mua, gợi ý, tư vấn, so sánh, tìm lựa chọn thay thế, nâng cấp, phụ kiện hoặc hỏi thông tin về bất kỳ sản phẩm nào. Cũng dùng khi người dùng tham chiếu sản phẩm trong lịch sử như “mẫu này”, “con thứ hai”, “hai mẫu trên”")]
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
