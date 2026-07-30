using System.ComponentModel;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class ProductByIdsRequest
{
    [Description(
        "Danh sách canonical productId lấy nguyên từ productReferences trong conversation context hoặc từ kết quả function trước đó. " +
        "Không truyền tên, URL, external ID hoặc ID tự tạo.")]
    public required List<string> ProductIds { get; init; }
}
