using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
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
        private readonly IBusinessQuotaRepository _businessQuotaRepository;
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;

        public ProductEmbedCommandHandler(
            ILogger<ProductEmbedCommandHandler> logger,
            IQdrantService qdrantService,
            IGeminiService geminiService,
            IProductRepository productRepository,
            IQwenService qwenService,
            IUnitOfWork unitOfWork,
            IBusinessQuotaRepository businessQuotaRepository,
            IUsageQuotaLogRepository usageQuotaLogRepository,
            TimeProvider timeProvider)
        {
            _logger = logger;
            _qdrantService = qdrantService;
            _geminiService = geminiService;
            _qwenService = qwenService;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _businessQuotaRepository = businessQuotaRepository;
            _usageQuotaLogRepository = usageQuotaLogRepository;

        }

        public async Task<Result<ProductResponse>> Handle(ProductEmbedCommand request, CancellationToken cancellationToken)
        {

            if (!ObjectId.TryParse(request.ProductId, out var productId))
            {
                return Result<ProductResponse>.Failure(400, "Invalid product ID.");
            }

            var product = await _productRepository.FindAsync(x =>
                x.Id == productId && x.Status != ProductStatus.Deleted);

            if (product == null) return Result<ProductResponse>.Failure(404, "Product not found.");

            if (product.Status != ProductStatus.PendingEmbedding)
            {
                return Result<ProductResponse>.Success(ProductMappings.ToResponse(product));
            }

            var embeddingText = product.SearchContent;

            if (embeddingText == null) return Result<ProductResponse>.Failure(404, "Product not found.");

            var sematicSearchText = await BuildSemanticSearchText(embeddingText, cancellationToken);

            if (!sematicSearchText.IsSuccess)
            {
                return Result<ProductResponse>.Failure(500, "Failed to build semantic search text.");
            }


            var productTechnicalVector = await _geminiService.EmbeddingsAsyncV2(
                embeddingText,
                "RETRIEVAL_DOCUMENT",
                cancellationToken);

            var productSemanticVector = await _geminiService.EmbeddingsAsyncV2(
                sematicSearchText.Data!.Result,
                "RETRIEVAL_DOCUMENT",
                cancellationToken);



            if (!productTechnicalVector.IsSuccess || !productSemanticVector.IsSuccess)
            {
                return Result<ProductResponse>.Failure(500, "Failed to generate embeddings.");
            }

            var currentBusinessQuota = await _businessQuotaRepository.GetCurrentBusinessQuota(product.BusinessId);

            if (currentBusinessQuota == null)
                return Result<ProductResponse>.Failure(404, "Business quota not found", null, BusinessQuotaMessageCode.NotFound);

            var totalTokenEmbebProduct =
                 (long)Math.Ceiling(productTechnicalVector.Data!.InputTokens / 3.0)
                 + (long)Math.Ceiling(productSemanticVector.Data!.InputTokens / 3.0)
                 + (long)Math.Ceiling(sematicSearchText.Data.InputTokens / 3.0);

            var geminiCredits =
                totalTokenEmbebProduct +
                sematicSearchText.Data.OutputTokens * 2;

            var newUsageQuotaLog = new UsageQuotaLog
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = product.BusinessId,
                InputTokens = totalTokenEmbebProduct,
                BillableTokens = geminiCredits,
                BusinessQuotaId = currentBusinessQuota.Id,
                MessageUsed = 0,
                OutputTokens = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                SourceId = product.Id,
                SourceType = SourceTypeEnum.EmbeddingProduct,
            };

            currentBusinessQuota.UsedTokens += totalTokenEmbebProduct;

            var embeddedAt = _timeProvider.GetUtcNow();
            product.Status = ProductStatus.Active;
            product.EmbbbedAt = embeddedAt;

            var qdrantPoint = new PointStruct
            {
                Id = product.QdrantPointId,
                Vectors = new Vectors
                {
                    Vectors_ = new NamedVectors
                    {
                        Vectors =
                        {
                            [ProductVectorNames.ProductTechnical] = ToQdrantDenseVector(productTechnicalVector.Data!.Result),
                            [ProductVectorNames.SemanticSearch] = ToQdrantDenseVector(productSemanticVector.Data!.Result)
                        }
                    }
                },
            };

            foreach (var payloadItem in ProductMappings.BuildQdrantPayload(product))
            {
                qdrantPoint.Payload[payloadItem.Key] = payloadItem.Value;
            }

            try
            {
                await _qdrantService.UpsertAsync(
                    QdrantCollections.Products,
                    [qdrantPoint],
                    cancellationToken);

                await _productRepository.UpdateAsync(product);
                await _businessQuotaRepository.UpdateAsync(currentBusinessQuota);
                await _usageQuotaLogRepository.AddAsync(newUsageQuotaLog);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<ProductResponse>.Success(ProductMappings.ToResponse(product));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize product embedding.");
                return Result<ProductResponse>.Failure(500, "Failed to finalize product embedding.");
            }
        }



        private async Task<Result<GeminiResponse<string>>> BuildSemanticSearchText(
              string embeddingText,
              CancellationToken cancellationToken)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SemanticEmbedding.md");

            try
            {
                var response = await _geminiService.GenerateTextAsyncV2(new GeminiRequest
                {
                    Prompt = embeddingText,
                    GenerationConfig = new()
                    {
                        MaxOutputTokens = 600,
                        Temperature = 0.2
                    },
                    SystemPrompt = systemPrompt,

                }, cancellationToken);

                if (response.IsSuccess)
                {
                    return Result<GeminiResponse<string>>.Success(response.Data);
                }
                else
                {
                    throw new Exception(response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate semantic search text using Gemini.");
                return Result<GeminiResponse<string>>.Failure(500, "Failed to generate semantic search text using GeminiService.");
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
