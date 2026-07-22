using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IProductReferenceCollector
    {
        void AddRange(IEnumerable<ProductResponseV2> products);
        IReadOnlyList<ProductResponseV2> GetProducts();
        void Reset();
    }
}
