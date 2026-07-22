using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
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
        private readonly ILogger<ProductSemanticSearchQueryHandler> _logger;

        public ProductSemanticSearchQueryHandler(
            ICurrentUserService currentUserService,
            IGeminiService geminiService,
            IQdrantService qdrantService,
            ILogger<ProductSemanticSearchQueryHandler> logger,
            IProductRepository productRepository)
        {
            _currentUserService = currentUserService;
            _geminiService = geminiService;
            _qdrantService = qdrantService;
            _productRepository = productRepository;
            _logger = logger;
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

            var sw = Stopwatch.StartNew();

            var embeddingSemantic = await _geminiService.EmbeddingsAsyncV2(
                request.SemanticQuery,
                "RETRIEVAL_QUERY",
                ct);

            if (!embeddingSemantic.IsSuccess || embeddingSemantic.Data == null)
            {
                return Result<List<ProductResponse>>.Failure(
                    embeddingSemantic.StatusCode,
                    embeddingSemantic.Message,
                    embeddingSemantic.Errors,
                    embeddingSemantic.MessageCode);
            }

            double[]? technicalVector = null;
            if (request.TechnicalQuery != null)
            {
                var embeddingTechnical = await _geminiService.EmbeddingsAsyncV2(
                request.TechnicalQuery,
                "RETRIEVAL_QUERY",
                ct);


                if (!embeddingTechnical.IsSuccess || embeddingTechnical.Data == null)
                {
                    return Result<List<ProductResponse>>.Failure(
                        embeddingTechnical.StatusCode,
                        embeddingTechnical.Message,
                        embeddingTechnical.Errors,
                        embeddingTechnical.MessageCode);
                }

                technicalVector = embeddingTechnical.Data;
            }

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("4. Build 2 vertor: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");

            var filter = BuildFilter(business.Data.Id, request);

            sw = Stopwatch.StartNew();

            var points = await _qdrantService.HybridSearchAsync(
                embeddingSemantic: embeddingSemantic.Data.Select(x => (float)x).ToArray(),
                embeddingTechnical: technicalVector.Select(x => (float)x).ToArray(),
                filter: filter,
                candidateLimit: request.CandidateLimit,
                ct: ct);

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("5. Qdrant vector search: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");


            sw = Stopwatch.StartNew();

            var products = await LoadProductsAsync(points, business.Data.Id);

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("6. Load product: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");

            if (products.Count == 0)
            {
                return Result<List<ProductResponse>>.Success(
                    [],
                    200,
                    "No matching products found.");
            }

            sw = Stopwatch.StartNew();

            var reranked = await RerankAsync(request.SemanticQuery, products, request.TopK, ct);

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("7. Rernaking: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");

            if (!reranked.IsSuccess || reranked.Data == null)
            {
                return Result<List<ProductResponse>>.Failure(
                    reranked.StatusCode,
                    reranked.Message,
                    reranked.Errors,
                    reranked.MessageCode);
            }

            return Result<List<ProductResponse>>.Success(
                reranked.Data.Select(ProductMappings.ToResponse).ToList(),
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

    }
}
