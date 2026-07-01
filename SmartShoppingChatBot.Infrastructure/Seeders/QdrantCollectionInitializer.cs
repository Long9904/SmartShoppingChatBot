using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Infrastructure.Seeders
{
    public class QdrantCollectionInitializer
    {
        private readonly IQdrantService _qdrantService;
        private readonly GoogleConfigs _configs;

        public QdrantCollectionInitializer(IQdrantService qdrantService, IOptions<GoogleConfigs> configs)
        {
            _qdrantService = qdrantService;
            _configs = configs.Value;
        }

        public async Task EnsureAsync(CancellationToken ct = default)
        {
            await _qdrantService.EnsureCollectionAsync(
                QdrantCollections.Products,
                new VectorParamsMap
                {
                    Map =
                    {
                    [ProductVectorNames.ProductTechnical] = NewVectorParams(),
                    [ProductVectorNames.SemanticSearch] = NewVectorParams(),
                    }
                }, ct);

            // Ensure other collections if needed


        }


        private VectorParams NewVectorParams()
        {
            return new VectorParams
            {
                Size = (ulong)_configs.OutputDimensionality,
                Distance = Distance.Cosine
            };
        }
    }
}
