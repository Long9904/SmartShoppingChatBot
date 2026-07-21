using Qdrant.Client.Grpc;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IQdrantService
    {
        Task EnsureCollectionAsync(
            string collectionName,
            VectorParamsMap vectorsConfig,
            CancellationToken ct);

        Task UpsertAsync(
             string collectionName,
             IReadOnlyList<PointStruct> points,
             CancellationToken ct = default);

        Task SetPayloadAsync(
             string collectionName,
             IReadOnlyList<ulong> ids,
             Dictionary<string, Value> payload,
             CancellationToken ct = default);

        Task<IReadOnlyList<ScoredPoint>> SearchAsync(
            string collectionName,
            ReadOnlyMemory<float> embedding,
            Filter? filter,
            int limit,
            CancellationToken ct = default);

        Task<List<ScoredPoint>> HybridSearchAsync(
            float[] embeddingSemantic,
            float[] embeddingTechnical,
            Filter filter,
            int candidateLimit,
            CancellationToken ct);
        Task<List<ScoredPoint>> HybridDocumentSearchAsync(
            float[] embeddingSemantic,
            float[] embeddingTechnical,
            int candidateLimit,
            Filter filter,
            CancellationToken ct);
    }
}
