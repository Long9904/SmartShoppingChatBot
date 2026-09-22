using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public sealed class ProductReferenceResolverV3(IProductRepository products) : IProductReferenceResolverV3
{
    public async Task<IReadOnlyDictionary<string, ProductReferenceV3>> ResolveAsync(
        ObjectId businessId, IEnumerable<string> productIds,
        IEnumerable<ProductReferenceV3>? knownProducts = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ids = productIds.Where(id => ObjectId.TryParse(id, out _))
            .Select(id => ObjectId.Parse(id).ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, ProductReferenceV3>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in knownProducts ?? [])
        {
            if (ids.Contains(product.ProductId)) result[product.ProductId] = product.Copy();
        }

        var missing = ids.Where(id => !result.ContainsKey(id)).Select(ObjectId.Parse).ToList();

        if (missing.Count > 0)
        {
            var found = await products.FindAllAsync(p => missing.Contains(p.Id)
                && p.BusinessId == businessId && p.Status == ProductStatus.Active);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var product in found)
            {
                result[product.Id.ToString()] = ProductReferenceV3.FromProduct(product);
            }
        }
        return result;
    }

    public IReadOnlyList<ProductReferenceV3> GetInOrder(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, ProductReferenceV3> products)
        => ids
        .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).Where(products.ContainsKey)
        .Select(id => products[id].Copy()).ToList();
}
