using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreateEmbed
{
    public class ProductEmbedCommandHandler : IRequestHandler<ProductEmbedCommand, Result<ProductResponse>>
    {
        private readonly ILogger<ProductEmbedCommandHandler> _logger;
        private readonly IQdrantService _qdrantService;
        private readonly IGeminiService _geminiService;
        private readonly IQwenService _qwenService;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public ProductEmbedCommandHandler(
            ILogger<ProductEmbedCommandHandler> logger,
            IQdrantService qdrantService,
            IGeminiService geminiService,
            IProductRepository productRepository,
            IQwenService qwenService,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _qdrantService = qdrantService;
            _geminiService = geminiService;
            _qwenService = qwenService;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;

        }

        public async Task<Result<ProductResponse>> Handle(ProductEmbedCommand request, CancellationToken cancellationToken)
        {

            var product = await _productRepository.FindAsync(x => x.Id == ObjectId.Parse(request.ProductId));

            if (product == null) return Result<ProductResponse>.Failure(404, "Product not found.");

            var embeddingText = product.SearchContent;

            if (embeddingText == null) return Result<ProductResponse>.Failure(404, "Product not found.");

            var sematicSearchText = await BuildSematicSearchText(embeddingText);

            if (!sematicSearchText.IsSuccess)
            {
                return Result<ProductResponse>.Failure(500, "Failed to build semantic search text.");
            }


            var productTechnicalVector = await _geminiService.EmbeddingsAsyncV2(
                embeddingText,
                "RETRIEVAL_DOCUMENT",
                cancellationToken);

            var productSemanticVector = await _geminiService.EmbeddingsAsyncV2(
                sematicSearchText.Data!,
                "RETRIEVAL_DOCUMENT",
                cancellationToken);



            if (!productTechnicalVector.IsSuccess || !productSemanticVector.IsSuccess)
            {
                return Result<ProductResponse>.Failure(500, "Failed to generate embeddings.");
            }

            var embeddedAt = _timeProvider.GetUtcNow();
            product.Status = Domain.Enums.ProductStatus.Active;
            product.EmbbbedAt = embeddedAt;
            product.UpdatedAt = embeddedAt;

            var qdrantPoint = new PointStruct
            {
                Id = product.QdrantPointId,
                Vectors = new Vectors
                {
                    Vectors_ = new NamedVectors
                    {
                        Vectors =
                        {
                            [ProductVectorNames.ProductTechnical] = ToQdrantDenseVector(productTechnicalVector.Data!),
                            [ProductVectorNames.SemanticSearch] = ToQdrantDenseVector(productSemanticVector.Data!)
                        }
                    }
                },
                Payload =
                {
                    [ProductPayloadNames.ProductId] = product.Id.ToString(),
                    [ProductPayloadNames.BusinessId] = product.BusinessId.ToString(),
                    [ProductPayloadNames.Price] = (double)product.Price,
                    [ProductPayloadNames.Status] = product.Status.ToString(),
                    ["mongo_id"] = product.Id.ToString(),
                    ["business_id"] = product.BusinessId.ToString(),
                    ["external_id"] = product.ExternalId,
                    ["name"] = product.Name,
                    ["description"] = product.Description ?? "",
                    ["external_url"] = product.ExternalProductUrl ?? "",
                    ["price"] = (double)product.Price,
                    ["currency"] = product.Currency,
                    ["brand"] = product.Brand ?? "",
                    ["category"] =  product.Category,
                    ["status"] = product.Status.ToString()
                }
            };

            foreach (var meta in product.Metadata)
            {
                qdrantPoint.Payload[$"meta_{meta.Key}"] = meta.Value.ToString()!;
            }

            try
            {
                await _qdrantService.UpsertAsync(
                    QdrantCollections.Products,
                    [qdrantPoint],
                    cancellationToken);

                await _productRepository.UpdateAsync(product);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<ProductResponse>.Success(new ProductResponse
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
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize product embedding.");
                return Result<ProductResponse>.Failure(500, "Failed to finalize product embedding.");
            }
        }



        private async Task<Result<string>> BuildSematicSearchText(string embeddingText)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticEmbedding.md");

            try
            {
                var response = await _geminiService.GenerateTextAsyncV2(new GeminiRequest
                {
                    Prompt = embeddingText,
                    GenerationConfig = new()
                    {
                        MaxOutputTokens = 5000,
                        Temperature = 0.2
                    },
                    SystemPrompt = systemPrompt,
                });

                if (response.IsSuccess)
                {
                    return Result<string>.Success(response.Data);
                } else
                {
                    throw new Exception(response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate semantic search text using Gemini.");
                return Result<string>.Failure(500, "Failed to generate semantic search text using GeminiService.");
            }
        }



        private static Vector ToQdrantDenseVector(IEnumerable<double> values)
        {
            var denseVector = new DenseVector();
            denseVector.Data.Add(values.Select(x => (float)x));

            return new Vector
            {
                Dense = denseVector
            };
        }
    }
}
