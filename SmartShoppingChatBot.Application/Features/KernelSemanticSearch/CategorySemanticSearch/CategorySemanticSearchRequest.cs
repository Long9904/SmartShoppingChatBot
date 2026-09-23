using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CategoryPriceBand
{
    Any,
    Low,
    Medium,
    High
}

public sealed class CategoryAttributeFilterRequest
{
    [Description("Key chính xác của thuộc tính có IsFilterable=true từ Category.GetCategorySchemas, ví dụ color.")]
    public string Name { get; init; } = string.Empty;

    [Description("Sao chép nguyên văn AllowedValues trong schema của danh mục tương ứng: màu đen có thể là black, không tự dùng mau_den hoặc màu đen. Nếu AllowedValues rỗng, dùng giá trị đúng DataType khách đã nói rõ.")]
    public string Value { get; init; } = string.Empty;

    [Description("true chỉ cho sở thích phong cách/hoàn cảnh sử dụng (sang trọng, đi tiệc), được bỏ khỏi filter khi tìm vector/BM25 nhưng vẫn giữ trong query. false cho điều kiện bắt buộc như màu, size, chất liệu, brand khách chỉ rõ. IsStrict=true trong schema luôn được giữ.")]
    public bool IsPreference { get; init; }
}

public sealed class CategorySemanticSearchRequest
{
    [Description("Tên danh mục chính xác trong danh sách danh mục hợp lệ được cung cấp.")]
    public string Category { get; init; } = string.Empty;

    [Description("Từ khóa cho truy vấn BM25 nội bộ. BrowseProductsByCategory luôn để null; AI dùng SemanticProductSearch cho tìm kiếm đầy đủ.")]
    public string? Bm25Query { get; init; }

    [Description("Các thuộc tính khách nói rõ. Để mảng rỗng nếu khách chỉ nói loại sản phẩm.")]
    public List<CategoryAttributeFilterRequest> Attributes { get; init; } = [];

    [Description("Any nếu không nói giá; Low cho giá rẻ/bình dân; Medium cho tầm trung/trung bình; High cho giá cao/cao cấp.")]
    public CategoryPriceBand PriceBand { get; init; } = CategoryPriceBand.Any;

    [Description("Giá tối thiểu bằng số khi khách nói rõ ngân sách. Không tự điền từ phân khúc giá chung.")]
    public decimal? MinPrice { get; init; }

    [Description("Giá tối đa bằng số khi khách nói rõ ngân sách. Không tự điền từ phân khúc giá chung.")]
    public decimal? MaxPrice { get; init; }

    [Description("Canonical productId đã hiển thị cần loại trừ khi khách yêu cầu mẫu khác.")]
    public List<string> ExcludeProductIds { get; init; } = [];
}
