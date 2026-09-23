using System.ComponentModel;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;

public sealed class ProductGetByExternalIdsRequest
{
    [Description("Danh sách externalProductId từ hệ thống bán hàng. Không truyền canonical productId vào đây.")]
    public List<string> ExternalProductIds { get; init; } = [];
}
