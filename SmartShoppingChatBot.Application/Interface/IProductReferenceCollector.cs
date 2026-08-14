using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IProductReferenceCollector
    {
        void AddRange(IEnumerable<ProductResponseV2> products);
        void AddRange(IEnumerable<ProductResponseV3> products);
        IReadOnlyList<ProductResponseV3> GetProducts();
        void Reset();
    }
}
