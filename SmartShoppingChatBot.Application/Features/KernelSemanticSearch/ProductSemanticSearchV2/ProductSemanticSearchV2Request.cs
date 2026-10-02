using System.ComponentModel;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;

public sealed class ProductSemanticSearchV2Request
{
    [Description("Nhu cầu tự nhiên đầy đủ để tạo semantic vector. Không chứa giá vì giá được lọc bằng trường riêng.")]
    public string SemanticQuery { get; init; } = string.Empty;

    [Description("true khi nhu cầu có phong cách/hoàn cảnh hoặc ý nghĩa cần đánh giá (streetwear đi làm, đi tiệc sang trọng), kể cả khi schema không có key tương ứng. Khi true server tổng hợp category, vector và BM25 ngay lần đầu để AI đánh giá đủ ứng viên.")]
    public bool RequiresSemanticMatch { get; init; }

    [Description("Cụm mô tả catalogue ngắn gọn để tạo technical vector: loại, tên, thương hiệu và thông số khách đã nêu.")]
    public string TechnicalQuery { get; init; } = string.Empty;

    [Description("Cụm từ khóa ngắn cho BM25; ưu tiên tên/loại sản phẩm và thương hiệu. Ví dụ nhu cầu áo thun streetwear đi làm dùng áo thun, không nhét mọi từ phong cách/hoàn cảnh hoặc giá vào đây.")]
    public string Bm25Query { get; init; } = string.Empty;

    [Description("Danh mục từ danh sách trong prompt đã lấy schema. Server lọc category trước; nếu không có kết quả phù hợp thì mở rộng tìm vector và cuối cùng BM25. Rỗng khi không có danh mục/schema phù hợp.")]
    public string Category { get; init; } = string.Empty;

    [Description("Thuộc tính có Key và AllowedValues chính xác từ Category.GetCategorySchemas. Chỉ dùng khi danh mục có schema.")]
    public List<CategoryAttributeFilterRequest> Attributes { get; init; } = [];

    [Description("Any nếu không nói giá; Low cho giá rẻ/bình dân; Medium cho tầm trung; High cho cao cấp.")]
    public CategoryPriceBand PriceBand { get; init; } = CategoryPriceBand.Any;

    [Description("Giá tối thiểu bằng số; null nếu khách không nêu ngân sách cụ thể.")]
    public decimal? MinPrice { get; init; }

    [Description("Giá tối đa bằng số; null nếu khách không nêu ngân sách cụ thể.")]
    public decimal? MaxPrice { get; init; }

    [Description("Danh sách canonical productId phải loại khỏi kết quả; không truyền externalProductId vào đây.")]
    public List<string> ExcludeProductIds { get; init; } = [];
}
