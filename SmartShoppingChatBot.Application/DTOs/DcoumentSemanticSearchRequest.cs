using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class DocumentSemanticSearchRequest
    {
        [Description(@"
            Chuỗi tìm kiếm dùng để tạo embedding khi tìm kiếm nội dung tài liệu.

            Quy tắc:
            - Giữ nguyên ý định câu hỏi của người dùng.
            - Nếu câu hỏi phụ thuộc ngữ cảnh (ví dụ: 'nó', 'phần đó', 'chính sách này'), thay đại từ bằng thực thể hoặc chủ đề đã được nhắc trước đó.
            - Tập trung vào nội dung cần tra cứu trong tài liệu, chính sách, hướng dẫn, điều khoản hoặc FAQ.
            - Không trả lời câu hỏi.
            - Không tự suy diễn thông tin không có trong câu hỏi hoặc ngữ cảnh.
            - Không thêm tên tài liệu nếu người dùng không nhắc tới.
            ")]
        public string Query { get; set; } = string.Empty;

        [Description("Số lượng đoạn tài liệu lấy trước khi reranking. Khuyến nghị: 20-100. Mặc định: 20.")]
        public int CandidateLimit { get; set; } = 20;

        [Description("Số lượng đoạn tài liệu trả về sau cùng. Khuyến nghị: 3-8. Mặc định: 5.")]
        public int TopK { get; set; } = 5;

        [Description(@"
            Id của tài liệu cần giới hạn tìm kiếm. Có thể để trống.

            Quy tắc:
            - Chỉ truyền DocumentId khi người dùng yêu cầu tìm trong một tài liệu cụ thể.
            - Nếu người dùng hỏi chung về toàn bộ kiến thức hoặc không chỉ rõ tài liệu, để trống.
            - Không tự tạo hoặc suy đoán DocumentId.
            ")]
        public string? DocumentId { get; set; }
    }
}