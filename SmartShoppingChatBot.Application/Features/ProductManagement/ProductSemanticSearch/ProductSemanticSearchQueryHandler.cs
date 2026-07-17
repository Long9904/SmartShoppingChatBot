using MediatR;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch
{
    public class ProductSemanticSearchQueryHandler
        : IRequestHandler<ProductSemanticSearchQuery, Result<List<ProductResponse>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IGeminiService _geminiService;
        private readonly IQdrantService _qdrantService;
        private readonly IProductRepository _productRepository;

        public ProductSemanticSearchQueryHandler(
            ICurrentUserService currentUserService,
            IGeminiService geminiService,
            IQdrantService qdrantService,
            IProductRepository productRepository)
        {
            _currentUserService = currentUserService;
            _geminiService = geminiService;
            _qdrantService = qdrantService;
            _productRepository = productRepository;
        }

        public Task<Result<List<ProductResponse>>> Handle(
            ProductSemanticSearchQuery query,
            CancellationToken cancellationToken)
        {
            return SearchAsync(query.Request, cancellationToken);
        }

        private async Task<Result<List<ProductResponse>>> SearchAsync(
            ProductSemanticSearchRequest request,
            CancellationToken ct)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess || business.Data == null)
            {
                return Result<List<ProductResponse>>.Failure(
                    business.StatusCode,
                    business.Message,
                    business.Errors,
                    business.MessageCode);
            }

            var embedding = await _geminiService.EmbeddingsAsyncV2(
                request.Query,
                "RETRIEVAL_QUERY",
                ct);

            if (!embedding.IsSuccess || embedding.Data == null)
            {
                return Result<List<ProductResponse>>.Failure(
                    embedding.StatusCode,
                    embedding.Message,
                    embedding.Errors,
                    embedding.MessageCode);
            }

            var filter = BuildFilter(business.Data.Id, request);
            var points = await _qdrantService.HybridSearchAsync(
                embedding.Data.Select(x => (float)x).ToArray(),
                filter,
                request.CandidateLimit,
                ct);

            var products = await LoadProductsAsync(points, business.Data.Id);
            if (products.Count == 0)
            {
                return Result<List<ProductResponse>>.Success(
                    [],
                    200,
                    "No matching products found.");
            }

            var reranked = await RerankAsync(request.Query, products, request.TopK, ct);
            if (!reranked.IsSuccess || reranked.Data == null)
            {
                return Result<List<ProductResponse>>.Failure(
                    reranked.StatusCode,
                    reranked.Message,
                    reranked.Errors,
                    reranked.MessageCode);
            }

            return Result<List<ProductResponse>>.Success(
                reranked.Data.Select(MapResponse).ToList(),
                200,
                "Product semantic search successfully.");
        }

        private static Filter BuildFilter(ObjectId businessId, ProductSemanticSearchRequest request)
        {
            var filter = new Filter();

            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = ProductPayloadNames.BusinessId,
                    Match = new Match { Keyword = businessId.ToString() }
                }
            });

            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = ProductPayloadNames.Status,
                    Match = new Match { Keyword = ProductStatus.Active.ToString() }
                }
            });

            if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
            {
                var range = new Qdrant.Client.Grpc.Range();

                if (request.MinPrice.HasValue)
                {
                    range.Gte = (double)request.MinPrice.Value;
                }

                if (request.MaxPrice.HasValue)
                {
                    range.Lte = (double)request.MaxPrice.Value;
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

            return filter;
        }

        private async Task<List<Product>> LoadProductsAsync(
            IEnumerable<ScoredPoint> points,
            ObjectId businessId)
        {
            var orderedIds = points
                .Select(GetProductId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (orderedIds.Count == 0)
            {
                return [];
            }

            var products = await _productRepository.FindAllAsync(x =>
                orderedIds.Contains(x.Id)
                && x.BusinessId == businessId
                && x.Status == ProductStatus.Active);

            var productById = products.ToDictionary(x => x.Id);

            return orderedIds
                .Where(productById.ContainsKey)
                .Select(id => productById[id])
                .ToList();
        }

        private async Task<Result<List<Product>>> RerankAsync(
            string query,
            IReadOnlyCollection<Product> products,
            int topK,
            CancellationToken ct)
        {
            var productById = products.ToDictionary(x => x.Id.ToString());
            var records = products.Select(x => new RankRecord
            {
                Id = x.Id.ToString(),
                Title = x.Name,
                Content = string.IsNullOrWhiteSpace(x.SearchContent)
                    ? x.BuildEmbeddingText()
                    : x.SearchContent
            });

            var reranked = await _geminiService.RerankerAsyncV2(query, records, ct);
            if (!reranked.IsSuccess || reranked.Data == null)
            {
                return Result<List<Product>>.Failure(
                    reranked.StatusCode,
                    reranked.Message,
                    reranked.Errors,
                    reranked.MessageCode);
            }

            var rankedProducts = reranked.Data
                .OrderByDescending(x => x.Score)
                .Where(x => productById.ContainsKey(x.Id))
                .Take(topK)
                .Select(x => productById[x.Id])
                .ToList();

            return Result<List<Product>>.Success(rankedProducts);
        }

        private static ObjectId? GetProductId(ScoredPoint point)
        {
            if (!point.Payload.TryGetValue(ProductPayloadNames.ProductId, out var value))
            {
                return null;
            }

            return ObjectId.TryParse(value.StringValue, out var productId)
                ? productId
                : null;
        }

        private static ProductResponse MapResponse(Product product)
        {
            return new ProductResponse
            {
                Id = product.Id.ToString(),
                BusinessId = product.BusinessId.ToString(),
                ExternalId = product.ExternalId,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Currency = product.Currency,
                Brand = product.Brand,
                StockQuantity = product.StockQuantity,
                Category = product.Category,
                Status = product.Status,
                Images = product.Images,
                Metadata = product.Metadata,
                CreatedAt = product.CreatedAt
            };
        }
    }
}
