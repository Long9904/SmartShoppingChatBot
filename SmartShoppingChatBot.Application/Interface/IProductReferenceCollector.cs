using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IProductReferenceCollector
    {
        void AddRange(IEnumerable<ProductResponse> products);
        IReadOnlyList<ProductResponse> GetProducts();
        void Reset();
    }
}
