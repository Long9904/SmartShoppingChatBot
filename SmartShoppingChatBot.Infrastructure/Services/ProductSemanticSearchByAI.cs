using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    using System.Globalization;
    using Microsoft.Extensions.Logging;
    using MongoDB.Bson;
    using Qdrant.Client;
    using Qdrant.Client.Grpc;
    using SmartShoppingChatBot.Application.Commons.Results;
    using SmartShoppingChatBot.Application.DTOs;
    using SmartShoppingChatBot.Domain.Entities;
    using SmartShoppingChatBot.Domain.Enums;
    using SmartShoppingChatBot.Domain.Interface;
    using SmartShoppingChatBot.Domain.QdrantConfig;
    using QdrantRange = global::Qdrant.Client.Grpc.Range;


    public sealed class ProductSemanticSearchByAI(
        ICurrentUserService currentUser,
        IRedisBusinessConfig businessConfig,
        IProductRepository products,
        ICategoryAttributeSchemaRepository schemas,
        IGeminiService gemini,
        IQdrantService search,
        QdrantClient qdrant,
        IProductReferenceResolverV3 resolver,
        ILogger<ProductSemanticSearchByAI> logger) : IProductSemanticSearchByAI
    {
        public Task<Result<List<ProductReferenceV3>>> BrowseCategoryAsync(
            ProductCategoryBrowseRequestV3 request,
            CancellationToken ct) => RunAsync(async () =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Category))
                {
                    throw new ArgumentException("An exact category is required.");
                }

                if (request.ExcludeProductIds is null
                    || request.ExcludeProductIds.Count > 100
                    || request.ExcludeProductIds.Any(id => !ObjectId.TryParse(id, out _)))
                {
                    throw new ArgumentException("Excluded product IDs are invalid.");
                }

                if (!Enum.IsDefined(request.PriceBand)
                    || request.MinPrice < 0
                    || request.MaxPrice < 0
                    || (request.MinPrice.HasValue
                        && request.MaxPrice.HasValue
                        && request.MinPrice > request.MaxPrice))
                {
                    throw new ArgumentException("Price bounds or price band are invalid.");
                }

                var business = await RequireBusinessAsync();
                var schema = await GetSchemaAsync(request.Category)
                    ?? throw new ArgumentException("Category must match an active schema exactly.");
                var config = await businessConfig.GetBusinessConfigAsync(ct)
                    ?? business.Config
                    ?? new BusinessConfig();
                var (minimum, maximum) = ResolvePriceRange(
                    request.PriceBand,
                    request.MinPrice,
                    request.MaxPrice,
                    config);

                var excludedIds = request.ExcludeProductIds
                    .Select(ObjectId.Parse)
                    .ToHashSet();

                var filter = new Filter();
                filter.Must.Add(Keyword(
                    ProductPayloadNames.BusinessId,
                    business.Id.ToString()));
                filter.Must.Add(Keyword(
                    ProductPayloadNames.Status,
                    ProductStatus.Active.ToString()));
                filter.Must.Add(Keyword(
                    ProductPayloadNames.Category,
                    schema.Category));

                if (minimum.HasValue || maximum.HasValue)
                {
                    var range = new QdrantRange();

                    if (minimum.HasValue)
                    {
                        range.Gte = (double)minimum.Value;
                    }

                    if (maximum.HasValue)
                    {
                        range.Lte = (double)maximum.Value;
                    }

                    filter.Must.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = ProductPayloadNames.Price,
                            Range = range
                        }
                    });
                }

                foreach (var excludedId in excludedIds)
                {
                    filter.MustNot.Add(Keyword(
                        ProductPayloadNames.ProductId,
                        excludedId.ToString()));
                }

                var resultLimit = Math.Max(1, config.TopKDocument ?? 5);
                var candidateLimit = (uint)Math.Min(
                    100,
                    Math.Max(20, resultLimit * 3));

                var scrollResponse = await qdrant.ScrollAsync(
                    collectionName: QdrantCollections.Products,
                    filter: filter,
                    limit: candidateLimit,
                    payloadSelector: true,
                    vectorsSelector: false,
                    cancellationToken: ct);

                var productIds = scrollResponse.Result
                    .Select(point => point.Payload.TryGetValue(
                            ProductPayloadNames.ProductId,
                            out var value)
                        && ObjectId.TryParse(value.StringValue, out var id)
                            ? (ObjectId?)id
                            : null)
                    .Where(id => id.HasValue && !excludedIds.Contains(id.Value))
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                if (productIds.Count == 0)
                {
                    return Result<List<ProductReferenceV3>>.Success(
                        [],
                        message: $"No active indexed product matched category '{schema.Category}' and the requested price conditions.");
                }

                var currentProducts = await products.FindAllAsync(product =>
                    productIds.Contains(product.Id)
                    && product.BusinessId == business.Id
                    && product.Status == ProductStatus.Active);

                // Keep nullable price checks outside the MongoDB expression. Some provider
                // versions cannot translate captured Nullable<T>.HasValue and Value reliably.
                currentProducts = currentProducts
                    .Where(product =>
                        (!minimum.HasValue || product.Price >= minimum.Value)
                        && (!maximum.HasValue || product.Price <= maximum.Value))
                    .ToList();

                var productsById = currentProducts.ToDictionary(product => product.Id);
                var orderedProducts = productIds
                    .Where(productsById.ContainsKey)
                    .Select(id => productsById[id])
                    .Take(resultLimit)
                    .Select(ProductReferenceV3.FromProduct)
                    .ToList();

                if (orderedProducts.Count == 0)
                {
                    return Result<List<ProductReferenceV3>>.Success(
                        [],
                        message: $"Category '{schema.Category}' has indexed points, but no product currently matches the status and price conditions in the business database.");
                }

                return Result<List<ProductReferenceV3>>.Success(orderedProducts);
            }, ct);

        public Task<Result<List<ProductReferenceV3>>> SearchAsync(
            ProductSearchRequestV3 request,
            CancellationToken ct) =>
            RunAsync(async () =>
            {
                var business = await RequireBusinessAsync();
                var config = await businessConfig.GetBusinessConfigAsync(ct) ?? business.Config ?? new BusinessConfig();
                return await SearchCoreAsync(request, business.Id, config, ct);
            }, ct);

        public Task<Result<List<ProductReferenceV3>>> GetByIdsAsync(ProductByIdsRequestV3 request, CancellationToken ct) =>
            RunAsync(async () =>
            {
                var business = await RequireBusinessAsync();
                if (request?.ProductIds is not { Count: > 0 and <= 20 }
                    || request.ProductIds.Any(id => !ObjectId.TryParse(id, out _)))
                    throw new ArgumentException("Provide between one and twenty valid product IDs.");
                var found = await resolver.ResolveAsync(business.Id, request.ProductIds, cancellationToken: ct);
                return Result<List<ProductReferenceV3>>.Success(resolver.GetInOrder(request.ProductIds, found).ToList());
            }, ct);

        public Task<Result<List<ProductReferenceV3>>> SearchPriceAlternativesAsync(
            ProductPriceAlternativeRequestV3 request, CancellationToken ct) => RunAsync(async () =>
            {
                if (request is null || request.Search is null || !Enum.IsDefined(request.Strategy))
                    throw new ArgumentException("A valid price strategy and search are required.");
                ValidateSearch(request.Search);
                var business = await RequireBusinessAsync();
                var reference = await RequireReferenceAsync(business.Id, request.ReferenceProductId);
                if (reference.Price <= 0) throw new ArgumentException("Reference product must have a positive price.");
                var config = await businessConfig.GetBusinessConfigAsync(ct) ?? business.Config ?? new BusinessConfig();
                var category = await GetReferenceCategoryAsync(reference, ct);
                if (category is null)
                    return Result<List<ProductReferenceV3>>.Failure(409, "Reference product has no indexed category. Re-embed it before requesting price alternatives.");
                if (!string.IsNullOrWhiteSpace(request.Search.Category)
                    && !string.Equals(category, request.Search.Category.Trim(), StringComparison.Ordinal))
                    throw new ArgumentException("A price alternative must use the reference product's indexed category.");

                // Preserve the existing relative price windows; the server reads the current price.
                var minimum = request.Strategy == ProductPriceStrategyV3.DownSell
                    ? reference.Price * 0.85m : reference.Price + 0.01m;
                var maximum = request.Strategy == ProductPriceStrategyV3.DownSell
                    ? Math.Max(0m, reference.Price - 0.01m) : reference.Price * 1.20m;
                var target = CopySearch(request.Search, category,
                    request.Search.ExcludeProductIds.Append(reference.Id.ToString()).ToList());
                return await SearchCoreAsync(target, business.Id, config, ct, minimum, maximum);
            }, ct);

        public Task<Result<List<ProductCrossSellGroupV3>>> CrossSellAsync(
            ProductCrossSellRequestV3 request, CancellationToken ct) => RunAsync(async () =>
            {
                if (request?.Targets is not { Count: > 0 and <= 3 }
                    || request.Targets.Any(target => target is null || string.IsNullOrWhiteSpace(target.Category)))
                    throw new ArgumentException("Provide one to three complementary searches with exact categories.");
                var business = await RequireBusinessAsync();
                var reference = await RequireReferenceAsync(business.Id, request.ReferenceProductId);
                var config = await businessConfig.GetBusinessConfigAsync(ct) ?? business.Config ?? new BusinessConfig();
                var referenceCategory = await GetReferenceCategoryAsync(reference, ct)
                    ?? reference.Category;
                var groups = new List<ProductCrossSellGroupV3>();
                var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { reference.Id.ToString() };

                // Process groups separately to keep one category from taking every result.
                // Sequential calls also avoid concurrent use of the scoped database context.
                foreach (var target in request.Targets)
                {
                    var result = await RunAsync(async () =>
                    {
                        var exclusions = (target.ExcludeProductIds ?? []).Concat(selectedIds).Distinct().ToList();
                        var crossSellTarget = BuildCrossSellTarget(
                            target,
                            reference,
                            referenceCategory,
                            exclusions);

                        return await SearchCoreAsync(
                            crossSellTarget,
                            business.Id,
                            config,
                            ct,
                            perGroupLimit: Math.Min(2, Math.Max(1, config.TopKDocument ?? 5)));
                    }, ct);
                    var found = result.Data ?? [];
                    foreach (var product in found) selectedIds.Add(product.ProductId);
                    groups.Add(new ProductCrossSellGroupV3
                    {
                        Category = target.Category!,
                        SearchNeed = target.SemanticQuery,
                        IsSuccess = result.IsSuccess,
                        Message = result.Message,
                        Products = found
                    });
                }

                // Respect the configured total count while keeping the first result of each group.
                var remaining = Math.Max(1, config.TopKDocument ?? 5);
                var balanced = groups.Select(group => new List<ProductReferenceV3>()).ToList();
                for (var index = 0; index < 2 && remaining > 0; index++)
                {
                    for (var groupIndex = 0; groupIndex < groups.Count && remaining > 0; groupIndex++)
                    {
                        if (groups[groupIndex].Products.Count <= index) continue;
                        balanced[groupIndex].Add(groups[groupIndex].Products[index]);
                        remaining--;
                    }
                }
                groups = groups.Select((group, index) => new ProductCrossSellGroupV3
                {
                    Category = group.Category,
                    SearchNeed = group.SearchNeed,
                    IsSuccess = group.IsSuccess,
                    Message = group.Message,
                    Products = balanced[index]
                }).ToList();
                // Group status distinguishes an empty category from a failed search.
                return Result<List<ProductCrossSellGroupV3>>.Success(groups);
            }, ct);

        private async Task<Result<List<ProductReferenceV3>>> SearchCoreAsync(
            ProductSearchRequestV3 request, ObjectId businessId, BusinessConfig config, CancellationToken ct,
            decimal? relativeMinimum = null, decimal? relativeMaximum = null, int? perGroupLimit = null)
        {
            ValidateSearch(request);
            var schema = await GetSchemaAsync(request.Category);
            var (minimum, maximum) = ResolvePriceRange(request, config);
            if (relativeMinimum.HasValue) minimum = Math.Max(minimum ?? 0m, relativeMinimum.Value);
            if (relativeMaximum.HasValue) maximum = Math.Min(maximum ?? decimal.MaxValue, relativeMaximum.Value);
            if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
                return Result<List<ProductReferenceV3>>.Success([], message: "No price range satisfies all requested constraints.");

            var filter = new Filter();
            filter.Must.Add(Keyword(ProductPayloadNames.BusinessId, businessId.ToString()));
            filter.Must.Add(Keyword(ProductPayloadNames.Status, ProductStatus.Active.ToString()));
            if (schema is not null) filter.Must.Add(Keyword(ProductPayloadNames.Category, schema.Category));
            if (minimum.HasValue || maximum.HasValue)
            {
                var range = new QdrantRange();
                if (minimum.HasValue) range.Gte = (double)minimum.Value;
                if (maximum.HasValue) range.Lte = (double)maximum.Value;
                filter.Must.Add(new Condition { Field = new FieldCondition { Key = ProductPayloadNames.Price, Range = range } });
            }
            var excluded = request.ExcludeProductIds.Select(ObjectId.Parse).ToHashSet();
            foreach (var id in excluded) filter.MustNot.Add(Keyword(ProductPayloadNames.ProductId, id.ToString()));

            var attributeText = ApplyAttributes(filter, schema, request.Attributes);
            var semanticQuery = request.SemanticQuery.Trim() + attributeText;
            var technicalQuery = request.TechnicalQuery.Trim() + attributeText;
            var points = await SearchCandidatesAsync(
                request,
                semanticQuery,
                technicalQuery,
                filter,
                ct);

            var ids = points.Select(point => point.Payload.TryGetValue(ProductPayloadNames.ProductId, out var id)
                    && ObjectId.TryParse(id.StringValue, out var parsed) ? (ObjectId?)parsed : null)
                .Where(id => id.HasValue && !excluded.Contains(id.Value)).Select(id => id!.Value).Distinct().ToList();
            if (ids.Count == 0) return Result<List<ProductReferenceV3>>.Success([]);

            // Verify tenant, status and live prices again after reading the current database records.
            var current = await products.FindAllAsync(p => ids.Contains(p.Id) && p.BusinessId == businessId
                && p.Status == ProductStatus.Active);
            var candidates = current.Where(p => (!minimum.HasValue || p.Price >= minimum.Value)
                && (!maximum.HasValue || p.Price <= maximum.Value)).ToList();
            if (candidates.Count == 0) return Result<List<ProductReferenceV3>>.Success([]);

            var ranked = await gemini.RerankerAsyncV2(semanticQuery, candidates.Select(p => new RankRecord
            {
                Id = p.Id.ToString(),
                Title = p.Name,
                Content = string.IsNullOrWhiteSpace(p.SearchContent) ? p.BuildEmbeddingText() : p.SearchContent
            }), ct);
            if (!ranked.IsSuccess || ranked.Data is null)
                return Result<List<ProductReferenceV3>>.Failure(502, "Failed to rank product results.");

            var byId = candidates.ToDictionary(p => p.Id.ToString(), StringComparer.OrdinalIgnoreCase);
            var accepted = ranked.Data.Result.Where(item => byId.ContainsKey(item.Id))
                .OrderByDescending(item => item.Score).DistinctBy(item => item.Id).ToList();

            // Keep every item returned by the reranker. The score only controls ordering;
            // the chat model checks the actual product facts against the customer's request.

            if (request.Sort != ProductSortV3.Relevance)
            {
                accepted = request.Sort == ProductSortV3.PriceLowToHigh
                    ? accepted.OrderBy(item => byId[item.Id].Price)
                        .ThenBy(item => byId[item.Id].Name)
                        .ToList()
                    : accepted.OrderByDescending(item => byId[item.Id].Price)
                        .ThenBy(item => byId[item.Id].Name)
                        .ToList();
            }

            var limit = perGroupLimit ?? config.TopKDocument ?? 5;
            var result = accepted
                .Take(Math.Max(1, limit))
                .Select(item =>
                {
                    var product = ProductReferenceV3.FromProduct(byId[item.Id]);
                    product.Score = item.Score;
                    return product;
                })
                .ToList();

            var message = result.Count == 0
                ? "The reranker did not return any valid product candidate."
                : null;

            return Result<List<ProductReferenceV3>>.Success(result, message: message);
        }

        private async Task<List<ScoredPoint>> SearchCandidatesAsync(
            ProductSearchRequestV3 request,
            string semanticQuery,
            string technicalQuery,
            Filter filter,
            CancellationToken ct)
        {
            if (request.Sort != ProductSortV3.Relevance)
            {
                var direction = request.Sort == ProductSortV3.PriceLowToHigh
                    ? Direction.Asc
                    : Direction.Desc;

                var points = await qdrant.QueryAsync(
                    collectionName: QdrantCollections.Products,
                    query: new OrderBy
                    {
                        Key = ProductPayloadNames.Price,
                        Direction = direction
                    },
                    filter: filter,
                    limit: 100,
                    payloadSelector: true,
                    cancellationToken: ct);

                return points.ToList();
            }

            var vectors = await gemini.EmbeddingsAsyncV3(
                [semanticQuery, technicalQuery],
                "RETRIEVAL_QUERY",
                ct);

            if (!vectors.IsSuccess || vectors.Data?.Result is not { Count: 2 })
            {
                throw new InvalidOperationException("Failed to generate product search vectors.");
            }

            var semanticVector = vectors.Data.Result[0]
                .Select(value => (float)value)
                .ToArray();
            var technicalVector = vectors.Data.Result[1]
                .Select(value => (float)value)
                .ToArray();

            try
            {
                var prefetch = new List<PrefetchQuery>
                {
                    new()
                    {
                        Query = semanticVector,
                        Using = ProductVectorNames.SemanticSearch,
                        Filter = filter,
                        Limit = 60
                    },
                    new()
                    {
                        Query = technicalVector,
                        Using = ProductVectorNames.ProductTechnical,
                        Filter = filter,
                        Limit = 40
                    },
                    new()
                    {
                        Query = new Document
                        {
                            Text = technicalQuery,
                            Model = "qdrant/bm25"
                        },
                        Using = ProductVectorNames.Bm25,
                        Filter = filter,
                        Limit = 40
                    }
                };

                var points = await qdrant.QueryAsync(
                    collectionName: QdrantCollections.Products,
                    query: Fusion.Rrf,
                    prefetch: prefetch,
                    limit: 40,
                    payloadSelector: true,
                    cancellationToken: ct);

                return points.ToList();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "BM25 product search is unavailable. Falling back to the existing dense search.");

                return await search.HybridSearchAsync(
                    semanticVector,
                    technicalVector,
                    filter,
                    40,
                    ct);
            }
        }

        private async Task<Business> RequireBusinessAsync()
        {
            var result = await currentUser.GetBusiness();
            if (!result.IsSuccess || result.Data is null) throw new UnauthorizedAccessException("Business is unavailable.");
            return result.Data;
        }

        private async Task<Product> RequireReferenceAsync(ObjectId businessId, string id)
        {
            if (!ObjectId.TryParse(id, out var productId)) throw new ArgumentException("Reference product ID is invalid.");
            return await products.FindAsync(p => p.Id == productId && p.BusinessId == businessId && p.Status == ProductStatus.Active)
                ?? throw new KeyNotFoundException("Reference product was not found.");
        }

        private async Task<string?> GetReferenceCategoryAsync(Product reference, CancellationToken ct)
        {
            // MongoDB keeps the source category; Qdrant holds the selected schema category.
            if (reference.QdrantPointId == Guid.Empty) return null;
            var points = await qdrant.RetrieveAsync(QdrantCollections.Products, reference.QdrantPointId,
                withPayload: true, withVectors: false, cancellationToken: ct);
            var point = points.FirstOrDefault();
            if (point is null
                || !point.Payload.TryGetValue(ProductPayloadNames.BusinessId, out var tenant)
                || tenant.StringValue != reference.BusinessId.ToString()
                || !point.Payload.TryGetValue(ProductPayloadNames.ProductId, out var id)
                || id.StringValue != reference.Id.ToString()) return null;
            return point.Payload.TryGetValue(ProductPayloadNames.Category, out var category) ? category.StringValue : null;
        }

        private async Task<CategoryAttributeSchema?> GetSchemaAsync(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;
            var active = await schemas.FindAllAsync(s => s.IsActive && s.Category == category.Trim());
            return active.OrderByDescending(s => s.Version).FirstOrDefault()
                ?? throw new ArgumentException("Category must match an active schema exactly.");
        }

        private static void ValidateSearch(ProductSearchRequestV3 request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.SemanticQuery)
                || string.IsNullOrWhiteSpace(request.TechnicalQuery)
                || request.SemanticQuery.Length > 1000 || request.TechnicalQuery.Length > 1000)
                throw new ArgumentException("Both product queries are required and must not exceed 1000 characters.");
            if (!Enum.IsDefined(request.PriceBand) || request.MinPrice < 0 || request.MaxPrice < 0
                || (request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice > request.MaxPrice))
                throw new ArgumentException("Price bounds or price band are invalid.");
            if (!Enum.IsDefined(request.Sort))
                throw new ArgumentException("Product sort is invalid.");
            if (request.ExcludeProductIds is null || request.ExcludeProductIds.Count > 100
                || request.ExcludeProductIds.Any(id => !ObjectId.TryParse(id, out _)))
                throw new ArgumentException("Excluded product IDs are invalid.");
            if (request.Attributes is null || request.Attributes.Count > 12
                || request.Attributes.Any(a => a is null || string.IsNullOrWhiteSpace(a.Key) || string.IsNullOrWhiteSpace(a.Value))
                || request.Attributes.Select(a => a.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Attributes.Count)
                throw new ArgumentException("Product attributes are invalid or duplicated.");
        }

        private static (decimal? Minimum, decimal? Maximum) ResolvePriceRange(ProductSearchRequestV3 request, BusinessConfig config)
            => ResolvePriceRange(
                request.PriceBand,
                request.MinPrice,
                request.MaxPrice,
                config);

        private static (decimal? Minimum, decimal? Maximum) ResolvePriceRange(
            ProductPriceBandV3 priceBand,
            decimal? minPrice,
            decimal? maxPrice,
            BusinessConfig config)
        {
            // A numeric budget is more precise than vague words such as cheap or premium.
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                return (minPrice, maxPrice);
            }

            var range = priceBand switch
            {
                ProductPriceBandV3.Low => ((decimal?)0m, config.LowPriceMaxLimit ?? 200000m),
                ProductPriceBandV3.Medium => (config.MediumPriceMinLimit ?? 200000m, (decimal?)(config.MediumPriceMaxLimit ?? 1000000m)),
                ProductPriceBandV3.High => (config.HighPriceMinLimit ?? 1000000m, (decimal?)null),
                _ => ((decimal?)null, (decimal?)null)
            };
            if (range.Item1 < 0 || range.Item2 < 0
                || (range.Item1.HasValue && range.Item2.HasValue && range.Item1.Value > range.Item2.Value))
                throw new InvalidOperationException("Business price limits are invalid.");
            return range;
        }

        private static string ApplyAttributes(Filter filter, CategoryAttributeSchema? schema, List<ProductAttributeFilterV3> attributes)
        {
            if (attributes.Count > 0 && schema is null) throw new ArgumentException("Attribute filters require a category.");
            var text = new List<string>();
            foreach (var attribute in attributes)
            {
                var definition = schema!.Attributes.FirstOrDefault(a => a.IsFilterable
                    && string.Equals(a.Key, attribute.Key.Trim(), StringComparison.Ordinal));
                if (definition is null || ReservedFields.Contains(definition.Key))
                    throw new ArgumentException($"Unsupported attribute: {attribute.Key}.");
                var value = attribute.Value.Trim();
                if (definition.AllowedValues is { Count: > 0 }
                    && !definition.AllowedValues.Contains(value, StringComparer.Ordinal))
                    throw new ArgumentException($"Unsupported value '{value}' for {definition.Key} in {schema.Category}. Use exactly one of AllowedValues: {string.Join(", ", definition.AllowedValues)}. Do not remove the required filter.");
                Condition condition;
                switch (definition.DataType)
                {
                    case AttributeDataType.Keyword:
                        condition = Keyword(definition.Key, value);
                        break;
                    case AttributeDataType.Number:
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                            throw new ArgumentException($"Invalid number for {definition.Key}.");
                        condition = new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = definition.Key,
                                Range = new QdrantRange { Gte = number, Lte = number }
                            }
                        };
                        break;
                    case AttributeDataType.Boolean:
                        if (!bool.TryParse(value, out var boolean)) throw new ArgumentException($"Invalid boolean for {definition.Key}.");
                        condition = new Condition { Field = new FieldCondition { Key = definition.Key, Match = new Match { Boolean = boolean } } };
                        break;
                    default: throw new ArgumentException("Unsupported attribute type.");
                }
                // Tool attributes represent requirements stated by the customer.
                // Styling suggestions stay in the semantic query and are not sent as attributes.
                filter.Must.Add(condition);
                text.Add($"{definition.DisplayName}: {value.Replace('_', ' ')}");
            }
            return text.Count == 0 ? string.Empty : ". " + string.Join(". ", text);
        }

        private static Condition Keyword(string key, string value) => new()
        {
            Field = new FieldCondition { Key = key, Match = new Match { Keyword = value } }
        };

        private static ProductSearchRequestV3 CopySearch(
            ProductSearchRequestV3 source,
            string? category,
            List<string> exclusions)
            => new()
            {
                SemanticQuery = source.SemanticQuery,
                TechnicalQuery = source.TechnicalQuery,
                Category = category,
                Attributes = source.Attributes,
                PriceBand = source.PriceBand,
                MinPrice = source.MinPrice,
                MaxPrice = source.MaxPrice,
                Sort = source.Sort,
                ExcludeProductIds = exclusions
            };

        private static ProductSearchRequestV3 BuildCrossSellTarget(
            ProductSearchRequestV3 target,
            Product reference,
            string referenceCategory,
            List<string> exclusions)
        {
            var sourceFacts = new List<string>
            {
                $"source product: {reference.Name}",
                $"source category: {referenceCategory}"
            };

            if (!string.IsNullOrWhiteSpace(reference.Brand))
            {
                sourceFacts.Add($"source brand: {reference.Brand}");
            }

            foreach (var item in reference.Metadata.Take(6))
            {
                var value = item.Value.Length > 80
                    ? item.Value[..80]
                    : item.Value;

                sourceFacts.Add($"source {item.Key}: {value}");
            }

            var semanticQuery = $"{target.SemanticQuery}. Complement {string.Join(", ", sourceFacts)}";
            if (semanticQuery.Length > 1000)
            {
                semanticQuery = semanticQuery[..1000];
            }

            return new ProductSearchRequestV3
            {
                SemanticQuery = semanticQuery,
                TechnicalQuery = target.TechnicalQuery,
                Category = target.Category,
                PriceBand = target.PriceBand,
                MinPrice = target.MinPrice,
                MaxPrice = target.MaxPrice,
                Attributes = target.Attributes,
                Sort = target.Sort,
                ExcludeProductIds = exclusions
            };
        }

        private async Task<Result<T>> RunAsync<T>(Func<Task<Result<T>>> action, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                return await action();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return Result<T>.Failure(400, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return Result<T>.Failure(403, exception.Message);
            }
            catch (KeyNotFoundException exception)
            {
                return Result<T>.Failure(404, exception.Message);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "V3 product search failed");
                return Result<T>.Failure(502, "Product search is temporarily unavailable.");
            }
        }

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
    }
}
