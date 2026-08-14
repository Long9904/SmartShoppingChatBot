using System.ComponentModel;
using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCrossSell;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductPriceAlternative;
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
            "Dùng cho xem, tìm, mua, gợi ý, tư vấn hoặc tìm lựa chọn mới không dựa trên một sản phẩm làm mốc. " +
            "Khi người dùng yêu cầu sản phẩm khác, lựa chọn tiếp theo hoặc sản phẩm tương tự không trùng, " +
            "truyền các productId đã hiển thị vào ExcludeProductIds. " +
            "Nếu khách muốn rẻ hơn hoặc cao cấp hơn một sản phẩm đã biết, dùng SearchPriceAlternatives. " +
            "Nếu khách muốn phụ kiện cho một sản phẩm đã biết, dùng SearchCompatibleAccessories. " +
            "Nếu chỉ cần dữ liệu mới nhất của sản phẩm đã biết, dùng GetProductsByIds.")]
        [return: Description(
          "Product search result. When IsSuccess is true and Data contains products, " +
          "you must present 3-5 products from Data and include their exact productId values " +
          "in selectedProductIds. Never claim that no products were found when Data is non-empty.")]
        public async Task<Result<List<ProductResponseV3>>> SemanticProductSearch(
            [Description("Bộ lọc cấu trúc")] ProductSemanticSearchRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ProductSemanticSearchQuery { Request = request }, cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }

        [KernelFunction]
        [Description(
            "Tìm sản phẩm thay thế theo giá so với một sản phẩm tham chiếu đã có canonical productId. " +
            "Dùng DownSell cho yêu cầu rẻ hơn, ngân sách thấp hơn hoặc tiết kiệm hơn; " +
            "dùng UpSell cho yêu cầu cao cấp hơn, mạnh hơn hoặc nâng cấp. " +
            "Truyền lại nhu cầu semantic đầy đủ đã xác định trước yêu cầu đổi mức giá từ conversation context; " +
            "đặt yêu cầu mới của lượt hiện tại trong AdditionalRequirements và không lặp nó trong semantic query. " +
            "Semantic query không chứa giá hoặc các cụm rẻ hơn, đắt hơn, cao cấp hơn. " +
            "Function tự lấy giá mới nhất và tự tính khoảng giá; không tự suy ra hoặc truyền giá tham chiếu. " +
            "Không dùng function này để tìm phụ kiện.")]
        [return: Description(
            "Danh sách sản phẩm đã được lọc theo khoảng giá tương đối và rerank. " +
            "Chỉ trình bày sản phẩm trong Data và sao chép productId chính xác vào selectedProductIds.")]
        public async Task<Result<List<ProductResponseV3>>> SearchPriceAlternatives(
            [Description("Sản phẩm tham chiếu, chiến lược giá, nhu cầu semantic từ conversation context và nhu cầu bổ sung")] ProductPriceAlternativeRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ProductPriceAlternativeQuery { Request = request },
                cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }

        [KernelFunction]
        [Description(
            "Tìm phụ kiện hoặc sản phẩm bổ trợ tương thích với một sản phẩm tham chiếu đã có canonical productId. " +
            "Dùng cho cross-sell như ốp lưng, sạc, cáp, tai nghe hoặc phụ kiện đi kèm. " +
            "Truyền nhu cầu semantic đầy đủ về phụ kiện từ conversation context, gồm mục đích sử dụng và thuộc tính khách đã nói rõ; " +
            "semantic query không chứa giá và không tự suy diễn. " +
            "Function tự tải tên, thương hiệu và danh mục mới nhất của sản phẩm tham chiếu để tìm kiếm. " +
            "Không dùng function này cho sản phẩm thay thế rẻ hơn hoặc cao cấp hơn.")]
        [return: Description(
            "Danh sách phụ kiện hoặc sản phẩm bổ trợ đã được tìm kiếm và rerank. " +
            "Chỉ trình bày sản phẩm trong Data và sao chép productId chính xác vào selectedProductIds.")]
        public async Task<Result<List<ProductResponseV3>>> SearchCompatibleAccessories(
            [Description("Sản phẩm tham chiếu, nhu cầu semantic và loại phụ kiện cần tìm")] ProductCrossSellRequest request,
            [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ProductCrossSellQuery { Request = request },
                cancellationToken);

            _productReferenceCollector.AddRange(result.Data ?? []);

            return result;
        }

        [KernelFunction]
        [Description(
            "Lấy dữ liệu mới nhất của nhiều sản phẩm bằng canonical productId. " +
            "Dùng khi người dùng muốn xem chi tiết hoặc so sánh sản phẩm đã có trong conversation context, " +
            "ví dụ ‘mẫu này’, ‘con thứ hai’, ‘hai mẫu trên’, hoặc yêu cầu so sánh các sản phẩm đã được hiển thị. " +
            "Không gọi function này trước SearchPriceAlternatives hoặc SearchCompatibleAccessories vì hai function đó tự tải sản phẩm tham chiếu. " +
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
