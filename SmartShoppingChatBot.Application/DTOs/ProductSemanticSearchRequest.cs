using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductSemanticSearchRequest
    {
        [Description(@"
            Chuỗi tìm kiếm dùng để tạo semantic embedding.

            Quy tắc:
            - Giữ nguyên ý định của người dùng.
            - Nếu câu hỏi phụ thuộc ngữ cảnh (ví dụ: 'cái đó', 'màu kia'), thay đại từ bằng thực thể đã được nhắc trước đó.
            - Không trả lời câu hỏi.
            - Không suy diễn hoặc thêm thuộc tính sản phẩm.
            - Không bao gồm điều kiện về giá (đã có field riêng).
            ")]
        public string SemanticQuery { get; init; } = string.Empty;


        [Description(@"
            Chuỗi tìm kiếm dùng để tạo technical embedding.

            Quy tắc:
            - Chỉ tạo DUY NHẤT một chuỗi.
            - Chỉ giữ lại các thông tin kỹ thuật hoặc thuộc tính sản phẩm mà người dùng đề cập.
            - Giữ nguyên tên thuộc tính nếu có (ví dụ: màu sắc, RAM, CPU, dung lượng, kích thước...).
            - Không trả lời câu hỏi.
            - Không suy diễn hoặc thêm thuộc tính.
            - Không bao gồm điều kiện về giá (đã có field riêng).
            - Có thể null nếu sematic query đã đủ 
            ")]
        public string? TechnicalQuery { get; init; } = string.Empty;


        [Description("Số lượng sản phẩm lấy trước khi reranking. Khuyến nghị: 20-100. Mặc định: 100.")]
        public int CandidateLimit { get; init; } = 100;

        [Description("Số lượng sản phẩm trả về sau cùng. Khuyến nghị: 4-10. Mặc định: 6.")]
        public int TopK { get; init; } = 6;

        [Description("Giá tối thiểu. Có thể để trống.")]
        public decimal? MinPrice { get; init; }

        [Description("Giá tối đa. Có thể để trống.")]
        public decimal? MaxPrice { get; init; }
    }
}