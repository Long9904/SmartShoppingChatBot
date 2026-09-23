using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;
using QdrantRange = Qdrant.Client.Grpc.Range;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;

public sealed class ProductSemanticSearchV2QueryHandler(
    ICurrentUserService currentUserService,
    ICategoryAttributeSchemaService categorySchemas,
    IRedisBusinessConfig redisBusinessConfig,
    IGeminiService geminiService,
    IQdrantService qdrantService,
    IProductRepository productRepository,
    ILogger<ProductSemanticSearchV2QueryHandler> logger)
    : IRequestHandler<ProductSemanticSearchV2Query, Result<List<ProductReferenceV2>>>
{
    public async Task<Result<List<ProductReferenceV2>>> Handle(
        ProductSemanticSearchV2Query query,
        CancellationToken cancellationToken)
    {
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
            var schema = string.IsNullOrEmpty(category)
                ? null
                : await categorySchemas.GetActiveSchemaAsync(category);

            if (schema is null && request.Attributes.Count > 0)
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    400,
                    $"Danh mục '{category}' không có schema đang hoạt động nên không thể áp dụng Attributes.");
            }

            if (schema is null && !string.IsNullOrEmpty(category))
            {
                logger.LogInformation(
                    "Category {Category} has no active schema; product search starts with vector fallback.",
                    category);
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
                excludedProductIds);
            if (!filterResult.IsSuccess || filterResult.Data is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(
                    filterResult.StatusCode,
                    filterResult.Message,
                    filterResult.Errors,
                    filterResult.MessageCode);
            }

            var resultLimit = Math.Clamp(config.TopKDocument ?? 5, 1, 20);
            var candidateLimit = Math.Clamp(resultLimit * 8, 40, 100);
            logger.LogInformation(
                "Product search input: category={Category}, attributes={Attributes}, priceBand={PriceBand}, min={Minimum}, max={Maximum}, excluded={Excluded}, semanticMatch={SemanticMatch}, semantic={SemanticQuery}, bm25={Bm25Query}",
                category, JsonSerializer.Serialize(request.Attributes), request.PriceBand,
                priceRange.Minimum, priceRange.Maximum, excludedProductIds.Count,
                request.RequiresSemanticMatch, request.SemanticQuery, request.Bm25Query);

            async Task<Result<List<ProductReferenceV2>>> EvaluateAsync(
                IEnumerable<IDictionary<string, Value>> payloads, string stage)
            {
                var payloadById = new Dictionary<ObjectId, IDictionary<string, Value>>();
                var pointCount = 0;
                var invalidIdCount = 0;
                var excludedCount = 0;
                foreach (var payload in payloads)
                {
                    pointCount++;
                    if (!payload.TryGetValue(ProductPayloadNames.ProductId, out var value)
                        || !ObjectId.TryParse(value.StringValue, out var id))
                    {
                        invalidIdCount++;
                        continue;
                    }
                    if (excludedProductIds.Contains(id))
                    {
                        excludedCount++;
                        continue;
                    }
                    payloadById.TryAdd(id, payload);
                }
                logger.LogInformation(
                    "Product search stage {Stage}: Qdrant points={Points}, invalid productIds={InvalidIds}, excluded={Excluded}, usable={Usable}",
                    stage, pointCount, invalidIdCount, excludedCount, payloadById.Count);

                var ids = payloadById.Keys.ToList();
                if (ids.Count == 0)
                {
                    logger.LogInformation("Product search stage {Stage}: no candidates", stage);
                    return Result<List<ProductReferenceV2>>.Success([]);
                }

                var products = await productRepository.FindAllAsync(product =>
                    ids.Contains(product.Id)
                    && product.BusinessId == businessResult.Data.Id
                    && product.Status == ProductStatus.Active);
                var candidates = products.Where(product =>
                    (!priceRange.Minimum.HasValue || product.Price >= priceRange.Minimum.Value)
                    && (!priceRange.Maximum.HasValue || product.Price <= priceRange.Maximum.Value)
                    && (schema is null || !HasConflictingCanonicalMetadata(product, schema, request, stage != "category")))
                    .ToDictionary(product => product.Id);
                var orderedProducts = payloadById.Keys
                    .Where(candidates.ContainsKey)
                    .Select(id => candidates[id])
                    .Where(product => stage != "category" || MatchesNamedType(product, request.Bm25Query))
                    .Take(resultLimit)
                    .ToList();
                if (orderedProducts.Count == 0)
                {
                    logger.LogInformation("Product search stage {Stage}: no current matching products", stage);
                    return Result<List<ProductReferenceV2>>.Success([]);
                }

                var references = orderedProducts.Select((product, index) =>
                {
                    var reference = ProductReferenceV2.FromProduct(product);
                    reference.DisplayOrder = index + 1;
                    reference.QdrantPayload = ProductQdrantPayloadReader.FromPayload(payloadById[product.Id]);
                    return reference;
                }).ToList();
                logger.LogInformation(
                    "Product search stage {Stage}: {Candidates} current candidates, {Returned} returned",
                    stage, candidates.Count, references.Count);
                logger.LogInformation("Product search stage {Stage}: returned products={Products}", stage,
                    JsonSerializer.Serialize(references.Select(product => new
                    {
                        product.ProductId, product.Name, product.Category, product.Price,
                        product.Metadata, product.QdrantPayload
                    })));
                return Result<List<ProductReferenceV2>>.Success(references,
                    message: $"Tìm sản phẩm ở bước {stage}.");
            }

            // First try the selected category and its validated key/value filters.
            // Embeddings are only generated when this stage has no suitable result.
            var recoveryCandidates = new List<ProductReferenceV2>();
            if (schema is not null)
            {
                var exactPoints = await qdrantService.ScrollAsync(
                    QdrantCollections.Products, filterResult.Data, (uint)candidateLimit, cancellationToken);
                var exactResult = await EvaluateAsync(exactPoints.Select(point => point.Payload), "category");
                if (!exactResult.IsSuccess) return exactResult;
                if (query.IncludeBm25Candidates) recoveryCandidates.AddRange(exactResult.Data ?? []);
                if (exactResult.Data is { Count: > 0 }
                    && !query.IncludeBm25Candidates
                    && !request.RequiresSemanticMatch && !request.Attributes.Any(attribute => attribute.IsPreference))
                    return exactResult;
            }

            // Preferences remain in the semantic query, not as mandatory payload matches.
            // Explicit requirements and schema-strict attributes remain hard filters.
            var fallbackFilterResult = BuildFilter(businessResult.Data.Id, schema, request,
                priceRange.Minimum, priceRange.Maximum, excludedProductIds, includeCategory: false);
            if (!fallbackFilterResult.IsSuccess || fallbackFilterResult.Data is null)
                return Result<List<ProductReferenceV2>>.Failure(
                    fallbackFilterResult.StatusCode, fallbackFilterResult.Message,
                    fallbackFilterResult.Errors, fallbackFilterResult.MessageCode);
            logger.LogInformation("Product fallback filter: {Filter}", fallbackFilterResult.Data.ToString());

            var vectorFailed = false;
            try
            {
                var vectors = await geminiService.EmbeddingsAsyncV3(
                    [request.SemanticQuery.Trim(), request.TechnicalQuery.Trim()],
                    "RETRIEVAL_QUERY", cancellationToken);
                if (vectors.IsSuccess && vectors.Data?.Result is { Count: 2 })
                {
                    var vectorPoints = await qdrantService.HybridSearchAsync(
                        vectors.Data.Result[0].Select(value => (float)value).ToArray(),
                        vectors.Data.Result[1].Select(value => (float)value).ToArray(),
                        fallbackFilterResult.Data, candidateLimit, cancellationToken);
                    var vectorResult = await EvaluateAsync(vectorPoints.Select(point => point.Payload), "vector");
                    if (!vectorResult.IsSuccess) return vectorResult;
                    if (query.IncludeBm25Candidates) recoveryCandidates.AddRange(vectorResult.Data ?? []);
                    else if (vectorResult.Data is { Count: > 0 }) return vectorResult;
                }
                else
                {
                    vectorFailed = true;
                    logger.LogWarning("Product vector stage failed: {Message}; trying BM25", vectors.Message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                vectorFailed = true;
                logger.LogWarning(exception, "Product vector stage failed; trying BM25");
            }

            var bm25Points = await qdrantService.SearchBm25Async(QdrantCollections.Products,
                request.Bm25Query.Trim(), fallbackFilterResult.Data, (uint)candidateLimit, cancellationToken);
            var bm25Result = await EvaluateAsync(bm25Points.Select(point => point.Payload), "bm25");
            if (query.IncludeBm25Candidates && bm25Result.IsSuccess)
            {
                recoveryCandidates.AddRange(bm25Result.Data ?? []);
                var combined = recoveryCandidates.DistinctBy(product => product.ProductId).ToList();
                for (var index = 0; index < combined.Count; index++) combined[index].DisplayOrder = index + 1;
                if (combined.Count > 0)
                    return Result<List<ProductReferenceV2>>.Success(combined,
                        message: "Đã kiểm tra lại category, vector và BM25. Chỉ chọn ứng viên thỏa nhu cầu hiện tại.");
            }
            if (!bm25Result.IsSuccess || bm25Result.Data is { Count: > 0 }) return bm25Result;
            if (vectorFailed)
                return Result<List<ProductReferenceV2>>.Failure(502,
                    "Bước vector tạm thời không khả dụng; BM25 chưa tìm thấy kết quả phù hợp.");

            return Result<List<ProductReferenceV2>>.Success(
                [], message: "Đã tìm theo category/key-value, vector và BM25 nhưng chưa có sản phẩm đủ phù hợp. Không giới thiệu sản phẩm khác mục tiêu.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Product semantic search V2 failed");
            return Result<List<ProductReferenceV2>>.Failure(
                502,
                "Tìm kiếm sản phẩm semantic tạm thời không khả dụng.");
        }
    }

    private static bool MatchesNamedType(Product product, string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = $"{product.Category} {product.Name} {product.Brand}";
        return terms.Length > 0 && terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static Result<Filter> BuildFilter(
        ObjectId businessId,
        CategoryAttributeSchema? schema,
        ProductSemanticSearchV2Request request,
        decimal? minimumPrice,
        decimal? maximumPrice,
        IReadOnlySet<ObjectId> excludedProductIds,
        bool includeCategory = true)
    {
        var filter = new Filter();
        filter.Must.Add(Keyword(ProductPayloadNames.BusinessId, businessId.ToString()));
        filter.Must.Add(Keyword(ProductPayloadNames.Status, ProductStatus.Active.ToString()));

        if (includeCategory && schema is not null)
        {
            filter.Must.Add(Keyword(ProductPayloadNames.Category, schema.Category));
        }

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
                return Result<Filter>.Failure(400, $"Thuộc tính '{definition.DisplayName}' bị lặp.");
            }

            var condition = BuildAttributeCondition(definition, requestedAttribute.Value.Trim());
            if (!condition.IsSuccess || condition.Data is null)
            {
                return Result<Filter>.Failure(
                    condition.StatusCode,
                    condition.Message,
                    condition.Errors,
                    condition.MessageCode);
            }

            if (includeCategory || !requestedAttribute.IsPreference || definition.IsStrict)
                filter.Must.Add(condition.Data);
        }

        foreach (var excludedProductId in excludedProductIds)
        {
            filter.MustNot.Add(Keyword(ProductPayloadNames.ProductId, excludedProductId.ToString()));
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
            return Result<Condition>.Failure(
                400,
                $"Giá trị '{requestedValue}' không hợp lệ cho '{definition.Key}'. AllowedValues: {string.Join(", ", definition.AllowedValues)}.");
        }

        switch (definition.DataType)
        {
            case AttributeDataType.Keyword:
                return Result<Condition>.Success(Keyword(definition.Key, requestedValue));
            case AttributeDataType.Number:
                if (!double.TryParse(requestedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
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
        ProductSemanticSearchV2Request request,
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
            if (!ObjectId.TryParse(productId?.Trim(), out var parsedId)) return null;
            result.Add(parsedId);
        }

        return result;
    }

    private static bool HasConflictingCanonicalMetadata(
        Product product,
        CategoryAttributeSchema schema,
        ProductSemanticSearchV2Request request,
        bool isFallback)
    {
        return request.Attributes.Any(attribute =>
        {
            var definition = schema.Attributes.First(item => item.Key == attribute.Name.Trim());
            if (isFallback && attribute.IsPreference && !definition.IsStrict) return false;
            return ProductVariantAttributes.TryGetCanonicalKeyword(product, definition, out var canonical)
                && (canonical is null
                    || !string.Equals(canonical, attribute.Value.Trim(), StringComparison.Ordinal));
        });
    }

    private static Condition Keyword(string key, string value) => new()
    {
        Field = new FieldCondition
        {
            Key = key,
            Match = new Match { Keyword = value }
        }
    };

    private static readonly HashSet<string> ReservedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductPayloadNames.BusinessId,
        ProductPayloadNames.ProductId,
        ProductPayloadNames.Category,
        ProductPayloadNames.Status,
        ProductPayloadNames.Price,
        ProductPayloadNames.Name,
        ProductPayloadNames.ExternalId
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
