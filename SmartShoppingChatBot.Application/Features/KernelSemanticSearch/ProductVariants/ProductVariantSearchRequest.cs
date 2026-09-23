using System.ComponentModel;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductVariants;

public sealed class VariantAttributeChange
{
    [Description("Key chính xác của thuộc tính cần thay đổi trong schema danh mục, ví dụ color hoặc size.")]
    public string Name { get; init; } = string.Empty;

    [Description("Giá trị đích sao chép nguyên văn từ AllowedValues; null nếu khách chỉ hỏi giá trị khác mà không chỉ rõ giá trị nào.")]
    public string? Value { get; init; }
}

public sealed class ProductVariantSearchRequest
{
    [Description("Canonical productId của sản phẩm được nhắc tới trong productReferences, không phải externalProductId.")]
    public string ProductId { get; init; } = string.Empty;

    [Description("Một hoặc nhiều thuộc tính khách muốn thay đổi. Các thuộc tính filterable còn lại được giữ theo sản phẩm gốc.")]
    public List<VariantAttributeChange> ChangedAttributes { get; init; } = [];

    [Description("Giữ phân khúc giá từ yêu cầu khách: Any, Low, Medium hoặc High.")]
    public CategoryPriceBand PriceBand { get; init; } = CategoryPriceBand.Any;

    [Description("Giá tối thiểu khách nói rõ; không tự điền từ phân khúc giá.")]
    public decimal? MinPrice { get; init; }

    [Description("Giá tối đa khách nói rõ; không tự điền từ phân khúc giá.")]
    public decimal? MaxPrice { get; init; }
}
