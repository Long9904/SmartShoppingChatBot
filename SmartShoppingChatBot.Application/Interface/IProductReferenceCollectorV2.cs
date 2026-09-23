using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceCollectorV2
{
    void ResetFromV2();
    void AddRangeFromV2(IEnumerable<ProductReferenceV2> products);
    IReadOnlyList<ProductReferenceV2> GetProductsFromV2();
}
