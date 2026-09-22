using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public sealed class ProductReferenceCollectorV3 : IProductReferenceCollectorV3
{
    private readonly Dictionary<string, ProductReferenceV3> _products = new(StringComparer.OrdinalIgnoreCase);
    public void Reset() => _products.Clear();

    public void AddRange(IEnumerable<ProductReferenceV3> products)
    {
        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId)) continue;
            var snapshot = product.Copy();
            if (_products.TryGetValue(snapshot.ProductId, out var previous))
                snapshot.Score = Math.Max(previous.Score, snapshot.Score);
            _products[snapshot.ProductId] = snapshot;
        }
    }

    // Return copies so display order changes do not alter search snapshots.
    public IReadOnlyList<ProductReferenceV3> GetProducts() => _products.Values.Select(p => p.Copy()).ToList();
}
