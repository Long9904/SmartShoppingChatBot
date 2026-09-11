using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceResolver
{
    Task<IReadOnlyDictionary<string, ResolvedProductReference>> ResolveProductReferencesV2Async(
        ObjectId businessId,
        IEnumerable<string> productIds,
        IEnumerable<ProductResponseV2>? knownProducts = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ResolvedProductReference> GetInOrderProductV2(
        IEnumerable<string> productIds,
        IReadOnlyDictionary<string, ResolvedProductReference> productById);
}
