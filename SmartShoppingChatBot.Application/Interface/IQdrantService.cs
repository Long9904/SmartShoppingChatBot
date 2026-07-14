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
    }
}
