using System.Diagnostics;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
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
        : IRequestHandler<ProductSemanticSearchQuery, Result<List<ProductResponseV2>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IGeminiService _geminiService;
        private readonly IQdrantService _qdrantService;
        private readonly IProductRepository _productRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductSemanticSearchQueryHandler> _logger;
        private readonly IRedisBusinessConfig _redisBusinessConfig;

        public ProductSemanticSearchQueryHandler(
            ICurrentUserService currentUserService,
            IGeminiService geminiService,
            IQdrantService qdrantService,
            IMapper mapper,
            ILogger<ProductSemanticSearchQueryHandler> logger,
            IRedisBusinessConfig redisBusinessConfig,
            IProductRepository productRepository)
        {
            _currentUserService = currentUserService;
            _geminiService = geminiService;
            _qdrantService = qdrantService;
            _productRepository = productRepository;
            _redisBusinessConfig = redisBusinessConfig;
            _logger = logger;
            _mapper = mapper;
        }

        public Task<Result<List<ProductResponseV2>>> Handle(
            ProductSemanticSearchQuery query,
            CancellationToken cancellationToken)
        {
            return SearchAsync(query.Request, cancellationToken);
        }

        private async Task<Result<List<ProductResponseV2>>> SearchAsync(
            ProductSemanticSearchRequest request,
            CancellationToken ct)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess || business.Data == null)
            {
                return Result<List<ProductResponseV2>>.Failure(
                    business.StatusCode,
                    business.Message,
                    business.Errors,
                    business.MessageCode);
            }

            var sw = Stopwatch.StartNew();

            var buildVectors = await _geminiService.EmbeddingsAsyncV3(
                new[]
                {
                    request.SemanticQuery,
                    request.TechnicalQuery
                }, "RETRIEVAL_QUERY", ct);

            if (!buildVectors.IsSuccess || buildVectors.Data == null)
            {
                return Result<List<ProductResponseV2>>.Failure(
                    buildVectors.StatusCode,
                    buildVectors.Message,
                    buildVectors.Errors,
                    buildVectors.MessageCode);
            }

            //var embeddingSemantic = await _geminiService.EmbeddingsAsyncV2(
            //    request.SemanticQuery,
            //    "RETRIEVAL_QUERY",
            //    ct);

            //if (!embeddingSemantic.IsSuccess || embeddingSemantic.Data == null)
            //{
            //    return Result<List<ProductResponseV2>>.Failure(
            //        embeddingSemantic.StatusCode,
            //        embeddingSemantic.Message,
            //        embeddingSemantic.Errors,
            //        embeddingSemantic.MessageCode);
            //}

            //var embeddingTechnical = await _geminiService.EmbeddingsAsyncV2(
            //    request.TechnicalQuery,
            //    "RETRIEVAL_QUERY",
            //    ct);


            //if (!embeddingTechnical.IsSuccess || embeddingTechnical.Data == null)
            //{
            //    return Result<List<ProductResponseV2>>.Failure(
            //        embeddingTechnical.StatusCode,
            //        embeddingTechnical.Message,
            //        embeddingTechnical.Errors,
            //        embeddingTechnical.MessageCode);
            //}

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("4. Build 2 vertor: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");

            var filter = BuildFilter(business.Data.Id, request);

            sw = Stopwatch.StartNew();

            var points = await _qdrantService.HybridSearchAsync(
                embeddingSemantic: buildVectors.Data.Result[0].Select(x => (float)x).ToArray(),
                embeddingTechnical: buildVectors.Data.Result[1].Select(x => (float)x).ToArray(),
                filter: filter,
                candidateLimit: 40,
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
                return Result<List<ProductResponseV2>>.Success(
                    [],
                    200,
                    "No matching products found.");
            }

            sw = Stopwatch.StartNew();
            var busienssConfig = await _redisBusinessConfig.GetBusinessConfigAsync();


            var reranked = await RerankAsync(
                request.SemanticQuery,
                products,
                busienssConfig?.TopKDocument ?? 5,
                busienssConfig?.RerankingScore ?? 0.75,
                ct);

            sw.Stop();
            Console.WriteLine("----------------------------------");
            _logger.LogInformation("7. Rernaking: {kernel} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("----------------------------------");

            if (!reranked.IsSuccess || reranked.Data == null)
            {
                return Result<List<ProductResponseV2>>.Failure(
                    reranked.StatusCode,
                    reranked.Message,
                    reranked.Errors,
                    reranked.MessageCode);
            }

            var responses = _mapper.Map<List<ProductResponseV2>>(reranked.Data);

            return Result<List<ProductResponseV2>>.Success(
                responses,
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
            double score,
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

            var rankedProducts = reranked.Data.Result
                .OrderByDescending(x => x.Score)
                .Where(x => productById.ContainsKey(x.Id) && x.Score >= score)
                .Take(topK)
                .Select(x => productById[x.Id])
                .ToList();

            if (rankedProducts is null)
            {
                rankedProducts = reranked.Data.Result
                .OrderByDescending(x => x.Score)
                .Where(x => productById.ContainsKey(x.Id))
                .Take(topK)
                .Select(x => productById[x.Id])
                .ToList();
            }

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
