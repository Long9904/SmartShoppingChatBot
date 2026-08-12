using AutoMapper;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Commons.Services;

public sealed class ProductReferenceResolver : IProductReferenceResolver
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public ProductReferenceResolver(
        IProductRepository productRepository,
        IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyDictionary<string, ProductResponseV2>> ResolveAsync(
        ObjectId businessId,
        IEnumerable<string> productIds,
        IEnumerable<ProductResponseV2>? knownProducts = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestedIds = productIds
            .Where(productId => !string.IsNullOrWhiteSpace(productId))
            .Select(productId => productId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var requestedIdSet = requestedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productById = new Dictionary<string, ProductResponseV2>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in knownProducts ?? [])
        {
            if (!string.IsNullOrWhiteSpace(product.ProductId)
                && requestedIdSet.Contains(product.ProductId.Trim()))
            {
                productById[product.ProductId.Trim()] = product;
            }
        }

        var missingIds = requestedIds
            .Where(productId => !productById.ContainsKey(productId))
            .Select(productId => ObjectId.TryParse(productId, out var objectId)
                ? objectId
                : (ObjectId?)null)
            .Where(productId => productId.HasValue)
            .Select(productId => productId!.Value)
            .ToList();

        if (missingIds.Count > 0)
        {
            var products = await _productRepository.FindAllAsync(product =>
                missingIds.Contains(product.Id)
                && product.BusinessId == businessId
                && product.Status == ProductStatus.Active);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var product in _mapper.Map<List<ProductResponseV2>>(products))
            {
                productById[product.ProductId] = product;
            }
        }

        return productById;
    }

    public IReadOnlyList<ProductResponseV2> GetInOrder(
        IEnumerable<string> productIds,
        IReadOnlyDictionary<string, ProductResponseV2> productById)
    {
        var products = new List<ProductResponseV2>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawProductId in productIds)
        {
            var productId = rawProductId?.Trim();
            if (string.IsNullOrWhiteSpace(productId) || !seenIds.Add(productId))
            {
                continue;
            }

            if (productById.TryGetValue(productId, out var product))
            {
                products.Add(product);
            }
        }

        return products;
    }
}
