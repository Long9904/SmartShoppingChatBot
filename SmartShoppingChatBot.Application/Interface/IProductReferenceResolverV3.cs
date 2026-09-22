using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IProductReferenceResolverV3
{
    Task<IReadOnlyDictionary<string, ProductReferenceV3>> ResolveAsync(ObjectId businessId,
        IEnumerable<string> productIds, IEnumerable<ProductReferenceV3>? knownProducts = null,
        CancellationToken cancellationToken = default);
    IReadOnlyList<ProductReferenceV3> GetInOrder(IEnumerable<string> ids,
        IReadOnlyDictionary<string, ProductReferenceV3> products);
}
