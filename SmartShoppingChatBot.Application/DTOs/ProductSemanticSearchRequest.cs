using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductSemanticSearchRequest
    {
        [Description(
            "Nhu cầu tìm sản phẩm bằng ngôn ngữ tự nhiên, gồm đầy đủ loại sản phẩm, " +
            "mục đích, thương hiệu và thuộc tính đã được người dùng đề cập. " +
            "Giải quyết đại từ từ ngữ cảnh; không suy diễn và không chứa giá.")]
        public required string SemanticQuery { get; init; }

        [Description(
            "Cùng nhu cầu nhưng viết ngắn gọn, trực tiếp, gần với dữ liệu catalogue: " +
            "loại sản phẩm, tên, thương hiệu, danh mục và các thuộc tính được nêu rõ. " +
            "Không viết quảng cáo, không suy diễn và không chứa giá.")]
        public required string TechnicalQuery { get; init; }

        [Description("Giá tối thiểu; null nếu không yêu cầu.")]
        public decimal? MinPrice { get; init; }

        [Description("Giá tối đa; null nếu không yêu cầu.")]
        public decimal? MaxPrice { get; init; }

        [Description(
            "Danh sách canonical productId phải loại khỏi kết quả. " +
            "Dùng productId từ productReferences khi người dùng yêu cầu sản phẩm khác, lựa chọn tiếp theo, " +
            "hoặc sản phẩm tương tự nhưng không muốn lặp lại các sản phẩm đã hiển thị; dùng mảng rỗng nếu không cần loại trừ.")]
        public List<string> ExcludeProductIds { get; init; } = [];
    }
}
