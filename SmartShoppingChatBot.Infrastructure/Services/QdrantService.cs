using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class QdrantService : IQdrantService
    {
        private readonly QdrantClient _qdrantClient;
        private readonly ILogger<QdrantService> _logger;

        public QdrantService(QdrantClient qdrantClient, ILogger<QdrantService> logger)
        {
            _qdrantClient = qdrantClient;
            _logger = logger;
        }

        public async Task EnsureCollectionAsync(
            string collectionName,
            VectorParamsMap vectorsConfig,
            CancellationToken ct,
            SparseVectorConfig? sparseVectorsConfig = null)
        {
            var isCollectionExists = await _qdrantClient.CollectionExistsAsync(collectionName, ct);
            if (isCollectionExists) return;

            await _qdrantClient.CreateCollectionAsync(
                collectionName: collectionName,
                vectorsConfig: vectorsConfig,
                sparseVectorsConfig: sparseVectorsConfig,
                cancellationToken: ct);

            _logger.LogInformation("Collection {CollectionName} created successfully.", collectionName);
        }

        public async Task SetPayloadAsync(
            string collectionName,
            IReadOnlyList<Guid> ids,
            Dictionary<string, Value> payload,
            CancellationToken ct = default)
        {
            await _qdrantClient.SetPayloadAsync(
                collectionName: collectionName,
                payload: payload,
                ids: ids,
                cancellationToken: ct);
        }

        public async Task UpsertAsync(
            string collectionName,
            IReadOnlyList<PointStruct> points,
            CancellationToken ct = default)
        {
            await _qdrantClient.UpsertAsync(
                collectionName: collectionName,
                points: points,
                cancellationToken: ct);
        }



        public Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collectionName, ReadOnlyMemory<float> embedding, Filter? filter, int limit, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ScoredPoint>> HybridSearchAsync(
            float[] embeddingSemantic,
            float[] embeddingTechnical,
            Filter filter,
            int candidateLimit,
            CancellationToken ct)
        {
            const ulong semanticLimit = 60;
            const ulong technicalLimit = 40;

            var prefetch = new List<PrefetchQuery>
            {
                new()
                {
                    Query = embeddingSemantic,
                    Using = ProductVectorNames.SemanticSearch,
                    Filter = filter,
                    Limit = semanticLimit
                },
                new()
                {
                    Query = embeddingTechnical,
                    Using = ProductVectorNames.ProductTechnical,
                    Filter = filter,
                    Limit = technicalLimit
                }
            };

            var points = await _qdrantClient.QueryAsync(
                collectionName: QdrantCollections.Products,
                query: Fusion.Rrf,
                prefetch: prefetch,
                limit: (ulong)candidateLimit,
                payloadSelector: true,
                cancellationToken: ct);

            return points.ToList();
        }

        public async Task<List<ScoredPoint>> HybridProductSearchV2Async(
            float[] embeddingSemantic,
            float[] embeddingTechnical,
            string bm25Query,
            Filter filter,
            int candidateLimit,
            CancellationToken ct)
        {
            var prefetch = new List<PrefetchQuery>
            {
                new()
                {
                    Query = embeddingSemantic,
                    Using = ProductVectorNames.SemanticSearch,
                    Filter = filter,
                    Limit = 60
                },
                new()
                {
                    Query = embeddingTechnical,
                    Using = ProductVectorNames.ProductTechnical,
                    Filter = filter,
                    Limit = 40
                },
                new()
                {
                    Query = new Document
                    {
                        Text = bm25Query,
                        Model = "qdrant/bm25"
                    },
                    Using = ProductVectorNames.Bm25,
                    Filter = filter,
                    Limit = 40
                }
            };

            var points = await _qdrantClient.QueryAsync(
                collectionName: QdrantCollections.Products,
                query: Fusion.Rrf,
                prefetch: prefetch,
                limit: (ulong)candidateLimit,
                payloadSelector: true,
                cancellationToken: ct);

            return points.ToList();
        }

        public async Task<List<ScoredPoint>> HybridDocumentSearchAsync(
            float[] embeddingSemantic,
            float[] embeddingTechnical,
            int candidateLimit,
            Filter filter,
            CancellationToken ct)
        {
            const ulong semanticLimit = 60;
            const ulong technicalLimit = 40;

            var prefetch = new List<PrefetchQuery>
            {
                new()
                {
                    Query = embeddingSemantic,
                    Using = DocumentVectorNames.SemanticSearch,
                    Filter = filter,
                    Limit = semanticLimit
                },
                new()
                {
                    Query = embeddingTechnical,
                    Using = DocumentVectorNames.DocumentTechnical,
                    Filter = filter,
                    Limit = technicalLimit
                }
            };

            var points = await _qdrantClient.QueryAsync(
                collectionName: QdrantCollections.Documents,
                query: Fusion.Rrf,
                prefetch: prefetch,
                limit: (ulong)candidateLimit,
                payloadSelector: true,
                cancellationToken: ct);

            return points.ToList();
        }

        public async Task<IReadOnlyList<RetrievedPoint>> ScrollAsync(
            string collectionName,
            Filter filter,
            uint limit,
            CancellationToken ct = default)
        {
            var response = await _qdrantClient.ScrollAsync(
                collectionName: collectionName,
                filter: filter,
                limit: limit,
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: ct);

            return response.Result;
        }

        public async Task<IReadOnlyList<ScoredPoint>> SearchBm25Async(
            string collectionName,
            string query,
            Filter filter,
            uint limit,
            CancellationToken ct = default)
        {
            var points = await _qdrantClient.QueryAsync(
                collectionName: collectionName,
                query: new Document { Text = query, Model = "qdrant/bm25" },
                usingVector: ProductVectorNames.Bm25,
                filter: filter,
                limit: limit,
                payloadSelector: true,
                cancellationToken: ct);
            return points.ToList();
        }

        public async Task DeletePointsAsync(string collectionName, IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            await _qdrantClient.DeleteAsync(
                collectionName: collectionName,
                ids: ids,
                cancellationToken: ct);
        }
    }
}
