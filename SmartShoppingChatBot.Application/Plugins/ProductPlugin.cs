using System.ComponentModel;
using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;
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
        [Description(
            "Tìm kiếm sản phẩm mới theo nhu cầu ngôn ngữ tự nhiên khi chưa có canonical productId phù hợp. " +
            "Dùng cho xem, tìm, mua, gợi ý, tư vấn, tìm lựa chọn thay thế, nâng cấp hoặc phụ kiện. " +
            "Nếu sản phẩm đã có productId trong conversation context thì dùng GetProductsByIds thay vì function này.")]
        [return: Description("Product search result. Every product has a canonical productId that must be copied exactly when the product is referenced.")]
        public async Task<Result<List<ProductResponseV2>>> SemanticProductSearch(
            [Description("Bộ lọc cấu trúc")] ProductSemanticSearchRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ProductSemanticSearchQuery { Request = request }, cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }

        [KernelFunction]
        [Description(
            "Lấy dữ liệu mới nhất của nhiều sản phẩm bằng canonical productId. " +
            "Bắt buộc ưu tiên function này khi người dùng tham chiếu sản phẩm đã có trong conversation context, " +
            "ví dụ ‘mẫu này’, ‘con thứ hai’, ‘hai mẫu trên’, hoặc yêu cầu so sánh các sản phẩm đã được hiển thị. " +
            "Không dùng tên sản phẩm thay cho productId.")]
        [return: Description("Products found in the database, returned in the same order as the requested product IDs.")]
        public async Task<Result<List<ProductResponseV2>>> GetProductsByIds(
            [Description("Danh sách canonical productId cần lấy từ database")] ProductByIdsRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ProductGetByIdsQuery { ProductIds = request.ProductIds },
                cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }
    }
}
