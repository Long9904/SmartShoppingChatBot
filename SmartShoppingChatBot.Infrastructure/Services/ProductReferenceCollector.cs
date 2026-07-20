using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ProductReferenceCollector : IProductReferenceCollector
    {
        private readonly List<ProductResponse> _products = [];

        public void AddRange(IEnumerable<ProductResponse> products)
        {
            _products.AddRange(products);
        }

        public IReadOnlyList<ProductResponse> GetProducts()
        {
            return _products.ToList();
        }

        public void Reset()
        {
            _products.Clear();
        }
    }
}
