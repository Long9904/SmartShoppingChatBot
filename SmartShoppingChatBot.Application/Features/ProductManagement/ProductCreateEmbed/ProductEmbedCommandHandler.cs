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

        public ProductEmbedCommandHandler(
            ILogger<ProductEmbedCommandHandler> logger,
            IQdrantService qdrantService,
            IGeminiService geminiService,
            IProductRepository productRepository,
            IQwenService qwenService)
        {
            _logger = logger;
            _qdrantService = qdrantService;
            _geminiService = geminiService;
            _qwenService = qwenService;
            _productRepository = productRepository;

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


            var productTechnicalVector = await _geminiService.EmbeddingsAsync(embeddingText);

            var productSemanticVector = await _geminiService.EmbeddingsAsync(sematicSearchText.Data!);



            if (!productTechnicalVector.IsSuccess || !productSemanticVector.IsSuccess)
            {
                return Result<ProductResponse>.Failure(500, "Failed to generate embeddings.");
            }

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
                    ["mongo_id"] = product.Id.ToString(),
                    ["business_id"] = product.BusinessId.ToString(),
                    ["external_id"] = product.ExternalId,
                    ["name"] = product.Name,
                    ["description"] = product.Description ?? "",
                    ["external_url"] = product.ExternalProductUrl ?? "",
                    ["price"] = product.Price.ToString(),
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
                _logger.LogError(ex, "Failed to upsert product embedding to Qdrant.");
                return Result<ProductResponse>.Failure(500, "Failed to upsert product embedding to Qdrant.");
            }
        }



        private async Task<Result<string>> BuildSematicSearchText(string embeddingText)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticEmbedding.md");

            var prompt = systemPrompt + $"\n\nproduct data: \n{embeddingText}";

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
                }

                else
                {
                    var fallbackResponse = await _qwenService.GenerateTextAsync(prompt, 5000, 0.2, false);
                    return Result<string>.Success(fallbackResponse.Data);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate semantic search text using Gemini. Falling back to Qwen.");

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


                if (!response.IsSuccess)
                {
                    _logger.LogError("Qwen also failed to generate semantic search text.");
                    return Result<string>.Failure(500, "Failed to generate semantic search text using both QwenService and GeminiService.");
                }

                return Result<string>.Success(response.Data);
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
