using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Interface;

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
            CancellationToken ct)
        {
            var isCollectionExists = await _qdrantClient.CollectionExistsAsync(collectionName, ct);
            if (isCollectionExists) return;

            await _qdrantClient.CreateCollectionAsync(
                collectionName: collectionName,
                vectorsConfig: vectorsConfig,
                cancellationToken: ct);

            _logger.LogInformation("Collection {CollectionName} created successfully.", collectionName);
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
    }
}
