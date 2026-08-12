using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceResolver
{
    Task<IReadOnlyDictionary<string, ProductResponseV2>> ResolveAsync(
        ObjectId businessId,
        IEnumerable<string> productIds,
        IEnumerable<ProductResponseV2>? knownProducts = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ProductResponseV2> GetInOrder(
        IEnumerable<string> productIds,
        IReadOnlyDictionary<string, ProductResponseV2> productById);
}
