using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;
using QdrantRange = Qdrant.Client.Grpc.Range;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

public sealed class CategorySemanticSearchQueryHandler(
    ICurrentUserService currentUserService,
    ICategoryAttributeSchemaService categorySchemas,
    IRedisBusinessConfig redisBusinessConfig,
    IQdrantService qdrantService,
    IProductRepository productRepository,
    ILogger<CategorySemanticSearchQueryHandler> logger)
    : IRequestHandler<CategorySemanticSearchQuery, Result<List<ProductReferenceV2>>>
{
    public async Task<Result<List<ProductReferenceV2>>> Handle(
        CategorySemanticSearchQuery query,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Category calling---------------------");
        try
        {
            var businessResult = await currentUserService.GetBusiness();
            if (!businessResult.IsSuccess || businessResult.Data is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    businessResult.StatusCode,
                    businessResult.Message,
                    businessResult.Errors,
                    businessResult.MessageCode);
            }

            var request = query.Request;
            var category = request.Category.Trim();
            var bm25Query = request.Bm25Query?.Trim();
            var schema = string.IsNullOrEmpty(category)
                ? null
                : await categorySchemas.GetActiveSchemaAsync(category);
            if (schema is null && (string.IsNullOrEmpty(bm25Query) || request.Attributes.Count > 0))
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    400,
                    $"Danh mục '{category}' không có schema đang hoạt động; BM25 khi không có schema chỉ nhận Attributes=[].");
            }

            var excludedProductIds = ParseExcludedProductIds(request.ExcludeProductIds);
            if (excludedProductIds is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    400,
                    "ExcludeProductIds chứa canonical productId không hợp lệ.");
            }

            var config = await redisBusinessConfig.GetBusinessConfigAsync(cancellationToken)
                ?? businessResult.Data.Config
                ?? new BusinessConfig();
            var priceRange = ResolvePriceRange(request, config);
            if (!priceRange.IsValid)
            {
                return Result<List<ProductReferenceV2>>.Failure(400, priceRange.Error);
            }

            var filterResult = BuildFilter(
                businessResult.Data.Id,
                schema,
                request,
                priceRange.Minimum,
                priceRange.Maximum,
                excludedProductIds,
                includeCategory: string.IsNullOrEmpty(bm25Query));
            if (!filterResult.IsSuccess || filterResult.Data is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    filterResult.StatusCode,
                    filterResult.Message,
                    filterResult.Errors,
                    filterResult.MessageCode);
            }

            var resultLimit = Math.Clamp(config.TopKDocument ?? 5, 1, 20);
            var candidateLimit = (uint)Math.Clamp(resultLimit * 4, 20, 100);
            List<ObjectId> orderedProductIds;
            if (string.IsNullOrEmpty(bm25Query))
            {
                var points = await qdrantService.ScrollAsync(
                    QdrantCollections.Products, filterResult.Data, candidateLimit, cancellationToken);
                orderedProductIds = points.Select(point => GetProductId(point.Payload))
                    .Where(id => id.HasValue && !excludedProductIds.Contains(id.Value))
                    .Select(id => id!.Value).Distinct().ToList();
            }
            else
            {
                var points = await qdrantService.SearchBm25Async(
                    QdrantCollections.Products, bm25Query, filterResult.Data,
                    candidateLimit, cancellationToken);
                orderedProductIds = [];
                var seenProductIds = new HashSet<ObjectId>();
                foreach (var point in points)
                {
                    var id = GetProductId(point.Payload);
                    if (!id.HasValue || excludedProductIds.Contains(id.Value)
                        || !seenProductIds.Add(id.Value)) continue;
                    orderedProductIds.Add(id.Value);
                }
            }

            if (orderedProductIds.Count == 0)
            {
                return Result<List<ProductReferenceV2>>.Success(
                    [],
                    message: "Không tìm thấy sản phẩm phù hợp với bộ lọc danh mục.");
            }

            var products = await productRepository.FindAllAsync(product =>
                orderedProductIds.Contains(product.Id)
                && product.BusinessId == businessResult.Data.Id
                && product.Status == ProductStatus.Active);

            var currentProducts = products
                .Where(product =>
                    (!priceRange.Minimum.HasValue || product.Price >= priceRange.Minimum.Value)
                    && (!priceRange.Maximum.HasValue || product.Price <= priceRange.Maximum.Value)
                    && (string.IsNullOrEmpty(bm25Query)
                        || IsRelevantBm25Match(product, bm25Query))
                    && (schema is null || !HasConflictingCanonicalMetadata(product, schema, request, !string.IsNullOrEmpty(bm25Query))))
                .ToDictionary(product => product.Id);

            var references = orderedProductIds
                .Where(currentProducts.ContainsKey)
                .Select(id => ProductReferenceV2.FromProduct(currentProducts[id]))
                .Take(resultLimit)
                .Select((product, index) =>
                {
                    product.DisplayOrder = index + 1;
                    return product;
                })
                .ToList();

            logger.LogInformation("Category search {Category}: mode {Mode}, filters {@Attributes}, matched {Count} products",
                schema?.Category ?? "*", bm25Query is null ? "filter" : "bm25",
                request.Attributes, references.Count);

            return Result<List<ProductReferenceV2>>.Success(
                references,
                message: references.Count == 0
                    ? "Sản phẩm trong chỉ mục không còn khớp dữ liệu hiện tại."
                    : "Lọc sản phẩm theo danh mục thành công.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Category product search failed");
            return Result<List<ProductReferenceV2>>.Failure(
                502,
                "Tìm kiếm sản phẩm theo danh mục tạm thời không khả dụng.");
        }
    }

    private static Result<Filter> BuildFilter(
        ObjectId businessId,
        CategoryAttributeSchema? schema,
        CategorySemanticSearchRequest request,
        decimal? minimumPrice,
        decimal? maximumPrice,
        IReadOnlySet<ObjectId> excludedProductIds,
        bool includeCategory)
    {
        var filter = new Filter();
        filter.Must.Add(Keyword(ProductPayloadNames.BusinessId, businessId.ToString()));
        filter.Must.Add(Keyword(ProductPayloadNames.Status, ProductStatus.Active.ToString()));
        if (includeCategory && schema is not null)
            filter.Must.Add(Keyword(ProductPayloadNames.Category, schema.Category));

        if (minimumPrice.HasValue || maximumPrice.HasValue)
        {
            var range = new QdrantRange();
            if (minimumPrice.HasValue) range.Gte = (double)minimumPrice.Value;
            if (maximumPrice.HasValue) range.Lte = (double)maximumPrice.Value;
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = ProductPayloadNames.Price,
                    Range = range
                }
            });
        }

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requestedAttribute in request.Attributes)
        {
            var name = requestedAttribute.Name.Trim();
            var definition = schema?.Attributes.FirstOrDefault(attribute =>
                attribute.IsFilterable
                && string.Equals(attribute.Key, name, StringComparison.Ordinal));

            if (definition is null || ReservedFields.Contains(definition.Key))
            {
                return Result<Filter>.Failure(
                    400,
                    $"Thuộc tính '{name}' không hỗ trợ lọc trong danh mục '{schema?.Category}'. Gọi Category.GetCategorySchemas và dùng chính xác Key có IsFilterable=true.");
            }

            if (!usedKeys.Add(definition.Key))
            {
                return Result<Filter>.Failure(
                    400,
                    $"Thuộc tính '{definition.DisplayName}' bị lặp.");
            }

            var conditionResult = BuildAttributeCondition(
                definition,
                requestedAttribute.Value.Trim());
            if (!conditionResult.IsSuccess || conditionResult.Data is null)
            {
                return Result<Filter>.Failure(
                    conditionResult.StatusCode,
                    conditionResult.Message,
                    conditionResult.Errors,
                    conditionResult.MessageCode);
            }

            if (includeCategory || !requestedAttribute.IsPreference || definition.IsStrict)
                filter.Must.Add(conditionResult.Data);
        }

        foreach (var excludedProductId in excludedProductIds)
        {
            filter.MustNot.Add(Keyword(
                ProductPayloadNames.ProductId,
                excludedProductId.ToString()));
        }

        return Result<Filter>.Success(filter);
    }

    private static Result<Condition> BuildAttributeCondition(
        AttributeDefinition definition,
        string requestedValue)
    {
        if (definition.AllowedValues is { Count: > 0 }
            && !definition.AllowedValues.Contains(requestedValue, StringComparer.Ordinal))
        {
            return Result<Condition>.Failure(400,
                $"Giá trị '{requestedValue}' không hợp lệ cho '{definition.Key}'. AllowedValues: {string.Join(", ", definition.AllowedValues)}. Không bỏ điều kiện lọc; dùng đúng giá trị từ schema danh mục.");
        }

        switch (definition.DataType)
        {
            case AttributeDataType.Keyword:
                return Result<Condition>.Success(Keyword(definition.Key, requestedValue));

            case AttributeDataType.Number:
                if (!double.TryParse(
                        requestedValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number)
                    || !double.IsFinite(number))
                {
                    return Result<Condition>.Failure(
                        400,
                        $"Giá trị '{requestedValue}' không phải số hợp lệ cho '{definition.DisplayName}'.");
                }

                return Result<Condition>.Success(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = definition.Key,
                        Range = new QdrantRange { Gte = number, Lte = number }
                    }
                });

            case AttributeDataType.Boolean:
                if (!bool.TryParse(requestedValue, out var boolean))
                {
                    return Result<Condition>.Failure(
                        400,
                        $"Giá trị '{requestedValue}' phải là true hoặc false cho '{definition.DisplayName}'.");
                }

                return Result<Condition>.Success(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = definition.Key,
                        Match = new Match { Boolean = boolean }
                    }
                });

            default:
                return Result<Condition>.Failure(
                    400,
                    $"Kiểu dữ liệu của '{definition.DisplayName}' chưa được hỗ trợ.");
        }
    }

    private static PriceRangeResult ResolvePriceRange(
        CategorySemanticSearchRequest request,
        BusinessConfig config)
    {
        decimal? minimum;
        decimal? maximum;

        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
        {
            minimum = request.MinPrice;
            maximum = request.MaxPrice;
        }
        else
        {
            (minimum, maximum) = request.PriceBand switch
            {
                CategoryPriceBand.Low => ((decimal?)0m, config.LowPriceMaxLimit ?? 200000m),
                CategoryPriceBand.Medium => (
                    (decimal?)(config.MediumPriceMinLimit ?? 200000m),
                    config.MediumPriceMaxLimit ?? 1000000m),
                CategoryPriceBand.High => (config.HighPriceMinLimit ?? 1000000m, (decimal?)null),
                _ => ((decimal?)null, (decimal?)null)
            };
        }

        if (minimum < 0 || maximum < 0)
        {
            return PriceRangeResult.Invalid("Ngưỡng giá không được là số âm.");
        }

        if (minimum.HasValue && maximum.HasValue && minimum > maximum)
        {
            return PriceRangeResult.Invalid("Giá tối thiểu không được lớn hơn giá tối đa.");
        }

        return PriceRangeResult.Valid(minimum, maximum);
    }

    private static HashSet<ObjectId>? ParseExcludedProductIds(IEnumerable<string> productIds)
    {
        var result = new HashSet<ObjectId>();
        foreach (var productId in productIds)
        {
            if (!ObjectId.TryParse(productId?.Trim(), out var parsedId))
            {
                return null;
            }

            result.Add(parsedId);
        }

        return result;
    }

    private static ObjectId? GetProductId(
        Google.Protobuf.Collections.MapField<string, Value> payload)
    {
        if (!payload.TryGetValue(ProductPayloadNames.ProductId, out var value))
        {
            return null;
        }

        return ObjectId.TryParse(value.StringValue, out var productId)
            ? productId
            : null;
    }

    private static bool IsRelevantBm25Match(Product product, string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = $"{product.Category} {product.Name}";
        return terms.Length > 0 && terms.All(term =>
            title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static Condition Keyword(string key, string value) => new()
    {
        Field = new FieldCondition
        {
            Key = key,
            Match = new Match { Keyword = value }
        }
    };

    private static bool HasConflictingCanonicalMetadata(
        Product product, CategoryAttributeSchema schema, CategorySemanticSearchRequest request, bool isFallback)
    {
        // Source metadata can use a different vocabulary from the index. Only compare
        // values known to be canonical; never guess translations from raw metadata.
        return request.Attributes.Any(attribute =>
        {
            var definition = schema.Attributes.First(item => item.Key == attribute.Name.Trim());
            if (isFallback && attribute.IsPreference && !definition.IsStrict) return false;
            return ProductVariantAttributes.TryGetCanonicalKeyword(product, definition, out var canonical)
                && (canonical is null
                    || !string.Equals(canonical, attribute.Value.Trim(), StringComparison.Ordinal));
        });
    }

    private static readonly HashSet<string> ReservedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductPayloadNames.BusinessId, ProductPayloadNames.ProductId,
        ProductPayloadNames.Category, ProductPayloadNames.Status,
        ProductPayloadNames.Price, ProductPayloadNames.Name, ProductPayloadNames.ExternalId
    };

    private sealed record PriceRangeResult(
        bool IsValid,
        decimal? Minimum,
        decimal? Maximum,
        string? Error)
    {
        public static PriceRangeResult Valid(decimal? minimum, decimal? maximum)
            => new(true, minimum, maximum, null);

        public static PriceRangeResult Invalid(string error)
            => new(false, null, null, error);
    }
}
