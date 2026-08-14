using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ProductCrossSellRequest
{
    [Description(
        "Canonical productId của sản phẩm chính cần tìm phụ kiện tương thích. " +
        "Lấy chính xác từ productReferences trong conversation context.")]
    public required string ReferenceProductId { get; init; }

    [Description(
        "Nhu cầu semantic đầy đủ về phụ kiện lấy từ conversation context, gồm loại phụ kiện, " +
        "mục đích sử dụng và các thuộc tính khách đã nói rõ. Không chứa giá và không tự suy diễn.")]
    public required string SemanticQuery { get; init; }

    [Description(
        "Loại phụ kiện hoặc nhu cầu bổ sung được khách nói rõ, ví dụ ốp lưng, sạc hoặc tai nghe; " +
        "null nếu khách chỉ hỏi chung. Không tự suy diễn model sản phẩm.")]
    public string? AccessoryNeed { get; init; }

    [Description("Ngân sách tối đa khách nêu rõ cho phụ kiện; null nếu không có.")]
    public decimal? MaxPrice { get; init; }

    [Description(
        "Các canonical productId không được xuất hiện lại. " +
        "Dùng productId từ productReferences; mảng rỗng nếu không cần loại trừ.")]
    public List<string> ExcludeProductIds { get; init; } = [];
}
