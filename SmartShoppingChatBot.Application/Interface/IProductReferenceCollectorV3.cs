using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceCollectorV3
{
    void Reset();
    void AddRange(IEnumerable<ProductReferenceV3> products);
    IReadOnlyList<ProductReferenceV3> GetProducts();
}
