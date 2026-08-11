using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ProductReferenceCollector : IProductReferenceCollector
    {
        private readonly List<ProductResponseV2> _products = [];

        public void AddRange(IEnumerable<ProductResponseV2> products)
        {
            _products.AddRange(products);
        }

        public IReadOnlyList<ProductResponseV2> GetProducts()
        {
            return _products.ToList();
        }

        public void Reset()
        {
            _products.Clear();
        }
    }
}
