using System.ComponentModel;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ProductPriceAlternativeRequest
{
    [Description(
        "Canonical productId của sản phẩm làm mốc giá. " +
        "Lấy chính xác từ productReferences trong conversation context.")]
    public required string ReferenceProductId { get; init; }

    [Description(
        "DownSell khi khách muốn lựa chọn rẻ hơn; UpSell khi khách muốn lựa chọn cao cấp hoặc đắt hơn.")]
    public required PriceAlternativeStrategy Strategy { get; init; }

    [Description(
        "Nhu cầu semantic đầy đủ đã được xác định trước yêu cầu đổi mức giá hiện tại, lấy từ conversation context; " +
        "gồm loại sản phẩm, mục đích sử dụng và các thuộc tính khách đã yêu cầu trước đó. " +
        "Không lặp lại AdditionalRequirements, không chứa giá hoặc các cụm rẻ hơn, đắt hơn, cao cấp hơn và không tự suy diễn. Tối đa 200 kí tự")]
    public required string SemanticQuery { get; init; }

    [Description(
        "Các nhu cầu bổ sung được khách nói rõ như mục đích, thương hiệu hoặc tính năng; " +
        "null nếu không có. Không chứa giá và không tự suy diễn. Tối đa 150 kí tự")]
    public string? AdditionalRequirements { get; init; }

    [Description(
        "Các canonical productId không được xuất hiện lại. " +
        "Dùng productId từ productReferences; mảng rỗng nếu không cần loại trừ.")]
    public List<string> ExcludeProductIds { get; init; } = [];
}
