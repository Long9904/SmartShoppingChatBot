using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public class ProductReferenceCollectorV2 : IProductReferenceCollectorV2
{
    private readonly Dictionary<string, ProductReferenceV2> _products = new(StringComparer.OrdinalIgnoreCase);

    public void AddRangeFromV2(IEnumerable<ProductReferenceV2> products)
    {
        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId))
                continue;

            var snapshot = product.Copy();

            if (_products.TryGetValue(snapshot.ProductId, out var previous))
            {
                snapshot.Score = Math.Max(previous.Score, snapshot.Score);
            }

            _products[snapshot.ProductId] = snapshot;
        }
    }

    public IReadOnlyList<ProductReferenceV2> GetProductsFromV2() => _products.Values.Select(p => p.Copy()).ToList();


    public void ResetFromV2() => _products.Clear();
}
