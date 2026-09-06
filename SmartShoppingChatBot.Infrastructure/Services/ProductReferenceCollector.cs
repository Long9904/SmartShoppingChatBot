using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ProductReferenceCollector : IProductReferenceCollector
    {
        private readonly List<ProductResponseV2> _products = [];

        public void AddRange(IEnumerable<ProductResponseV2> products)
        {
            foreach (var product in products)
            {
                if (string.IsNullOrWhiteSpace(product.ProductId))
                {
                    continue;
                }

                var currentIndex = _products.FindIndex(current =>
                    string.Equals(
                        current.ProductId,
                        product.ProductId,
                        StringComparison.OrdinalIgnoreCase));
                var snapshot = product.Copy();

                if (currentIndex < 0)
                {
                    _products.Add(snapshot);
                    continue;
                }

                snapshot.Score = Math.Max(_products[currentIndex].Score, snapshot.Score);
                _products[currentIndex] = snapshot;
            }
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
