using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductSemanticSearchRequest
    {
        [Description(@"Ý định tìm kiếm để tạo embedding. 
            QUY TẮC BẮT BUỘC khi viết field này:
            - Nếu câu hỏi độc lập, rõ ràng: giữ nguyên gần như nguyên văn, chỉ sửa lỗi chính tả.
            - Nếu câu hỏi phụ thuộc ngữ cảnh trước đó (dùng đại từ như 'cái đó', 'màu kia'): 
              thay thế đại từ bằng thực thể cụ thể đã nhắc trước đó, KHÔNG thêm chi tiết mới không có trong hội thoại.
            - KHÔNG trả lời câu hỏi, KHÔNG suy diễn thêm thuộc tính sản phẩm nào ngoài những gì khách đã nói.
            - Không nhét ràng buộc giá vào đây — đã có field riêng.")]
        public string Query { get; init; } = string.Empty;

        [Description("Tổng số sản phẩm gần giống với yêu cầu nhất trước khi reranking. Range từ 20-100, trung bình 50")]
        public int CandidateLimit { get; init; } = 100;

        [Description("Top sản phẩm cần thiết. Trung bình là 4")]
        public int TopK { get; init; } = 10;

        [Description("Giá tối thiểu. Có thể trống")]
        public decimal? MinPrice { get; init; }

        [Description("Giá tôi đa. Có thể trống")]
        public decimal? MaxPrice { get; init; }
    }
}
