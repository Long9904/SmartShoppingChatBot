using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductVariants;

public sealed class ProductVariantSearchQueryHandler(
    ICurrentUserService currentUserService,
    ICategoryAttributeSchemaService categorySchemas,
    IRedisBusinessConfig redisBusinessConfig,
    IProductRepository products,
    IQdrantService qdrantService)
    : IRequestHandler<ProductVariantSearchQuery, Result<List<ProductReferenceV2>>>
{
    public async Task<Result<List<ProductReferenceV2>>> Handle(
        ProductVariantSearchQuery query, CancellationToken cancellationToken)
    {
        var business = await currentUserService.GetBusiness();
        if (!business.IsSuccess || business.Data is null)
            return Result<List<ProductReferenceV2>>.Failure(
                business.StatusCode, business.Message, business.Errors, business.MessageCode);

        var source = query.Source;
        if (!ObjectId.TryParse(source.ProductId, out var sourceId))
            return Result<List<ProductReferenceV2>>.Failure(400, "Canonical productId không hợp lệ.");

        if (!source.QdrantPayload.TryGetValue(ProductPayloadNames.Category, out var indexedCategory)
            || string.IsNullOrWhiteSpace(indexedCategory))
            return Result<List<ProductReferenceV2>>.Failure(409,
                "Sản phẩm gốc chưa có category trong Qdrant payload; không thể chọn schema từ danh mục nguồn MongoDB.");

        var schema = await categorySchemas.GetActiveSchemaAsync(indexedCategory);
        if (schema is null)
            return Result<List<ProductReferenceV2>>.Failure(400,
                $"Danh mục Qdrant '{indexedCategory}' không có schema đang hoạt động.");

        var changes = query.Request.ChangedAttributes ?? [];
        if (changes.Count == 0 || changes.Count > 10)
            return Result<List<ProductReferenceV2>>.Failure(400,
                "Cần chỉ rõ từ 1 đến 10 thuộc tính muốn thay đổi.");

        var changedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            var definition = schema.Attributes.FirstOrDefault(attribute =>
                attribute.IsFilterable && attribute.Key == change.Name?.Trim());
            if (definition is null || !changedKeys.Add(definition.Key))
                return Result<List<ProductReferenceV2>>.Failure(400,
                    $"Thuộc tính '{change.Name}' không hợp lệ hoặc bị lặp; dùng Key filterable trong schema.");

            if (PayloadValue(source.QdrantPayload, definition.Key) is null)
                return Result<List<ProductReferenceV2>>.Failure(400,
                    $"Qdrant payload của sản phẩm gốc thiếu '{definition.Key}', không thể xác định biến thể khác thuộc tính này.");

            if (!string.IsNullOrWhiteSpace(change.Value)
                && definition.AllowedValues is { Count: > 0 }
                && !definition.AllowedValues.Contains(change.Value.Trim(), StringComparer.Ordinal))
                return Result<List<ProductReferenceV2>>.Failure(400,
                    $"Giá trị '{change.Value}' không thuộc AllowedValues của '{definition.Key}': {string.Join(", ", definition.AllowedValues)}.");
        }

        var config = await redisBusinessConfig.GetBusinessConfigAsync(cancellationToken)
            ?? business.Data.Config ?? new BusinessConfig();
        var (minimum, maximum) = ResolvePriceRange(query.Request, config);
        if (minimum < 0 || maximum < 0 || (minimum.HasValue && maximum.HasValue && minimum > maximum))
            return Result<List<ProductReferenceV2>>.Failure(400, "Khoảng giá không hợp lệ.");

        var candidates = await products.FindAllAsync(product =>
            product.BusinessId == business.Data.Id
            && product.Status == ProductStatus.Active
            && product.Id != sourceId
            && product.Name == source.Name
            && product.Category == source.Category);

        var references = new List<ProductReferenceV2>();
        foreach (var candidate in candidates
                     .Where(product =>
                         string.Equals(product.Brand, source.Brand, StringComparison.OrdinalIgnoreCase)
                         && (!minimum.HasValue || product.Price >= minimum.Value)
                         && (!maximum.HasValue || product.Price <= maximum.Value))
                     .OrderBy(product => product.Price))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidatePayload = await ProductQdrantPayloadReader.LoadAsync(
                qdrantService, business.Data.Id, candidate.Id, cancellationToken);
            if (!string.Equals(PayloadValue(candidatePayload, ProductPayloadNames.Category),
                    indexedCategory, StringComparison.Ordinal)
                || !KeepsOtherAttributes(source.QdrantPayload, candidatePayload, schema, changedKeys)
                || !ChangesRequestedAttributes(source.QdrantPayload, candidatePayload, changes)
                || !MatchesDesiredValues(candidatePayload, changes))
                continue;

            var reference = ProductReferenceV2.FromProduct(candidate);
            reference.QdrantPayload = candidatePayload;
            reference.DisplayOrder = references.Count + 1;
            references.Add(reference);
            if (references.Count == 20) break;
        }

        return Result<List<ProductReferenceV2>>.Success(references,
            message: references.Count == 0
                ? "Không tìm thấy sản phẩm cùng mẫu với các thuộc tính yêu cầu."
                : "Đã tìm thấy sản phẩm cùng mẫu với các thuộc tính yêu cầu.");
    }

    private static bool KeepsOtherAttributes(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> candidate,
        CategoryAttributeSchema schema,
        IReadOnlySet<string> changedKeys)
    {
        foreach (var attribute in schema.Attributes.Where(attribute =>
                     attribute.IsFilterable && !changedKeys.Contains(attribute.Key)))
        {
            var sourceValue = PayloadValue(source, attribute.Key);
            if (sourceValue is not null
                && !string.Equals(sourceValue, PayloadValue(candidate, attribute.Key), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool ChangesRequestedAttributes(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> candidate,
        IReadOnlyList<VariantAttributeChange> changes)
        => changes.All(change =>
        {
            var sourceValue = PayloadValue(source, change.Name);
            var candidateValue = PayloadValue(candidate, change.Name);
            return candidateValue is not null
                && !string.Equals(sourceValue, candidateValue, StringComparison.Ordinal);
        });

    private static bool MatchesDesiredValues(
        IReadOnlyDictionary<string, string> payload,
        IReadOnlyList<VariantAttributeChange> changes)
        => changes.All(change => string.IsNullOrWhiteSpace(change.Value)
            || string.Equals(PayloadValue(payload, change.Name), change.Value.Trim(), StringComparison.Ordinal));

    private static string? PayloadValue(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static (decimal? Minimum, decimal? Maximum) ResolvePriceRange(
        ProductVariantSearchRequest request, BusinessConfig config)
    {
        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
            return (request.MinPrice, request.MaxPrice);

        return request.PriceBand switch
        {
            CategoryPriceBand.Low => (0m, config.LowPriceMaxLimit ?? 200000m),
            CategoryPriceBand.Medium => (config.MediumPriceMinLimit ?? 200000m,
                config.MediumPriceMaxLimit ?? 1000000m),
            CategoryPriceBand.High => (config.HighPriceMinLimit ?? 1000000m, null),
            _ => (null, null)
        };
    }
}
