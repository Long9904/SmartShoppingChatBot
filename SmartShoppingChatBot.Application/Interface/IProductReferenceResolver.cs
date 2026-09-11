using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceResolver
{
    Task<IReadOnlyDictionary<string, ProductResponseV2>> ResolveProductReferencesAsync(
        ObjectId businessId,
        IEnumerable<string> productIds,
        IEnumerable<ProductResponseV2>? knownProducts = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ProductResponseV2> GetInOrderProduct(
        IEnumerable<string> productIds,
        IReadOnlyDictionary<string, ProductResponseV2> productById);

    Task<IReadOnlyDictionary<string, ResolvedProductReference>> ResolveProductReferencesV2Async(
        ObjectId businessId,
        IEnumerable<string> productIds,
        IEnumerable<ProductResponseV2>? knownProducts = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ResolvedProductReference> GetInOrderProductV2(
        IEnumerable<string> productIds,
        IReadOnlyDictionary<string, ResolvedProductReference> productById);
}
