using System.Globalization;
using System.Text.Json;
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

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreateEmbed;

public class ProductEmbedCommandHandler : IRequestHandler<ProductEmbedCommand, Result<ProductResponse>>
{
    private readonly ILogger<ProductEmbedCommandHandler> _logger;
    private readonly IQdrantService _qdrantService;
    private readonly IGeminiService _geminiService;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IBusinessQuotaRepository _businessQuotaRepository;
    private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;
    private readonly ICategoryAttributeSchemaRepository _categoryAttributeSchemaRepository;
    private readonly IKernelChatService _kernelChatService;

    public ProductEmbedCommandHandler(
        ILogger<ProductEmbedCommandHandler> logger,
        IQdrantService qdrantService,
        IGeminiService geminiService,
        IProductRepository productRepository,
        IQwenService qwenService,
        IUnitOfWork unitOfWork,
        IBusinessQuotaRepository businessQuotaRepository,
        IUsageQuotaLogRepository usageQuotaLogRepository,
        ICategoryAttributeSchemaRepository categoryAttributeSchemaRepository,
        IKernelChatService kernelChatService,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _qdrantService = qdrantService;
        _geminiService = geminiService;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _businessQuotaRepository = businessQuotaRepository;
        _usageQuotaLogRepository = usageQuotaLogRepository;
        _categoryAttributeSchemaRepository = categoryAttributeSchemaRepository;
        _kernelChatService = kernelChatService;
    }

    public async Task<Result<ProductResponse>> Handle(
        ProductEmbedCommand request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ProductId, out var productId))
        {
            return Result<ProductResponse>.Failure(400, "Invalid product ID.");
        }

        var product = await _productRepository.FindAsync(x =>
            x.Id == productId && x.Status != ProductStatus.Deleted);

        if (product == null)
        {
            return Result<ProductResponse>.Failure(404, "Product not found.");
        }

        if (product.Status != ProductStatus.PendingEmbedding)
        {
            return Result<ProductResponse>.Success(ProductMappings.ToResponse(product));
        }

        var embeddingText = product.SearchContent;
        if (embeddingText == null)
        {
            return Result<ProductResponse>.Failure(404, "Product not found.");
        }

        var currentBusinessQuota = await _businessQuotaRepository.GetCurrentBusinessQuota(product.BusinessId);
        if (currentBusinessQuota == null)
        {
            return Result<ProductResponse>.Failure(
                404,
                "Business quota not found",
                null,
                BusinessQuotaMessageCode.NotFound);
        }

        // Product embedding events can be delivered more than once by RabbitMQ/MassTransit.
        // The UsageQuotaLogs index allows only one record per quota/product/type, so do not
        // charge and insert another log when this is a retry or a duplicate event.
        var existingUsageQuotaLog = await _usageQuotaLogRepository.FindAsync(x =>
            x.BusinessQuotaId == currentBusinessQuota.Id
            && x.SourceType == SourceTypeEnum.EmbeddingProduct
            && x.SourceId == product.Id);

        var remainingTokens = currentBusinessQuota.TokenLimit - currentBusinessQuota.UsedTokens;
        if (existingUsageQuotaLog == null
            && remainingTokens < ProductEmbeddingQuota.TokenBudgetPerProduct)
        {
            return Result<ProductResponse>.Failure(
                429,
                "Not enough token quota to embed product.",
                null,
                BusinessQuotaMessageCode.TokenLimitExceeded);
        }

        var activeSchemas = (await _categoryAttributeSchemaRepository.FindAllAsync(schema => schema.IsActive))
            .ToList();
        if (activeSchemas.Count == 0)
        {
            return Result<ProductResponse>.Failure(500, "No active category attribute schema is available.");
        }

        var semanticSearchText = await BuildSemanticSearchText(embeddingText, cancellationToken);
        if (!semanticSearchText.IsSuccess || semanticSearchText.Data is null)
        {
            return Result<ProductResponse>.Failure(500, "Failed to build semantic search text.");
        }

        _logger.LogInformation("Semantic search text generated for product {ProductId}.", product.Id);
        _logger.LogInformation("Semantic search text generated for product content: {content}", semanticSearchText.Data.Result);

        var selectedCategory = await SelectCategorySchemaAsync(product, activeSchemas, cancellationToken);
        if (!selectedCategory.IsSuccess || selectedCategory.Data is null)
        {
            return Result<ProductResponse>.Failure(500, "Failed to select a category schema.");
        }

        var selectedSchema = activeSchemas.FirstOrDefault(schema =>
            string.Equals(schema.Category, selectedCategory.Data.Result, StringComparison.Ordinal));
        if (selectedSchema is null)
        {
            return Result<ProductResponse>.Failure(500, "Selected category schema was not found.");
        }

        var selectedValues = await SelectCategoryValuesAsync(
            product,
            selectedSchema,
            semanticSearchText.Data.Result,
            cancellationToken);

        if (!selectedValues.IsSuccess || selectedValues.Data is null)
        {
            return Result<ProductResponse>.Failure(500, "Failed to select category attribute values.");
        }

        var categoryPayload = BuildCategoryPayload(product, selectedSchema, selectedValues.Data);

        var productTechnicalVector = await _geminiService.EmbeddingsAsyncV2(
            embeddingText,
            "RETRIEVAL_DOCUMENT",
            cancellationToken);

        var productSemanticVector = await _geminiService.EmbeddingsAsyncV2(
            semanticSearchText.Data.Result,
            "RETRIEVAL_DOCUMENT",
            cancellationToken);

        if (!productTechnicalVector.IsSuccess
            || productTechnicalVector.Data is null
            || !productSemanticVector.IsSuccess
            || productSemanticVector.Data is null)
        {
            return Result<ProductResponse>.Failure(500, "Failed to generate embeddings.");
        }

        var embeddingInputCredits =
            (long)Math.Ceiling(productTechnicalVector.Data.InputTokens / 3.0)
            + (long)Math.Ceiling(productSemanticVector.Data.InputTokens / 3.0)
            + (long)Math.Ceiling(semanticSearchText.Data.InputTokens / 3.0);
        var semanticOutputCredits = semanticSearchText.Data.OutputTokens * 2;

        var categoryCheckerInputCredits = (long)Math.Ceiling(selectedCategory.Data.InputTokens / 3.0);
        var categoryCheckerOutputCredits = selectedCategory.Data.OutputTokens * 2;
        var creditForCategoryChecker = categoryCheckerInputCredits + categoryCheckerOutputCredits;

        var categoryValueInputCredits = selectedValues.Data.InputTokens;
        var categoryValueOutputCredits = selectedValues.Data.OutputTokens * 6;
        var creditForCategoryValueSelection = categoryValueInputCredits + categoryValueOutputCredits;

        var totalInputCredits = embeddingInputCredits
            + categoryCheckerInputCredits
            + categoryValueInputCredits;
        var totalOutputCredits = semanticOutputCredits
            + categoryCheckerOutputCredits
            + categoryValueOutputCredits;
        var totalAiCredits = embeddingInputCredits
            + semanticOutputCredits
            + creditForCategoryChecker
            + creditForCategoryValueSelection;

        var newUsageQuotaLog = new UsageQuotaLog
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = product.BusinessId,
            InputTokens = totalInputCredits,
            BillableTokens = totalAiCredits,
            BusinessQuotaId = currentBusinessQuota.Id,
            MessageUsed = 0,
            OutputTokens = totalOutputCredits,
            CreatedAt = _timeProvider.GetUtcNow(),
            SourceId = product.Id,
            SourceType = SourceTypeEnum.EmbeddingProduct
        };

        if (existingUsageQuotaLog == null)
        {
            currentBusinessQuota.UsedTokens += totalAiCredits;
        }

        product.Status = ProductStatus.Active;
        product.EmbbbedAt = _timeProvider.GetUtcNow();

        var bm25Text = $"""
            Name: {product.Name}
            Brand: {product.Brand}
            Category: {product.Category}
            """;

        var qdrantPoint = new PointStruct
        {
            Id = product.QdrantPointId,
            Vectors = new Vectors
            {
                Vectors_ = new NamedVectors
                {
                    Vectors =
                    {
                        [ProductVectorNames.ProductTechnical] = ToQdrantDenseVector(productTechnicalVector.Data.Result),
                        [ProductVectorNames.SemanticSearch] = ToQdrantDenseVector(productSemanticVector.Data.Result),
                        [ProductVectorNames.Bm25] = new Vector
                        {
                            Document = new Document
                            {
                                Text = bm25Text,
                                Model = "qdrant/bm25"
                            }
                        }
                    }
                }
            }
        };

        foreach (var payloadItem in ProductMappings.BuildQdrantPayload(product))
        {
            qdrantPoint.Payload[payloadItem.Key] = payloadItem.Value;
        }

        qdrantPoint.Payload[ProductPayloadNames.Category] = selectedSchema.Category;
        foreach (var payloadItem in categoryPayload)
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

            if (existingUsageQuotaLog == null)
            {
                await _businessQuotaRepository.UpdateAsync(currentBusinessQuota);
                await _usageQuotaLogRepository.AddAsync(newUsageQuotaLog);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ProductResponse>.Success(ProductMappings.ToResponse(product));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to finalize product embedding.");
            return Result<ProductResponse>.Failure(500, "Failed to finalize product embedding.");
        }
    }

    private async Task<Result<GeminiResponse<string>>> SelectCategorySchemaAsync(
        Product product,
        IReadOnlyCollection<CategoryAttributeSchema> schemas,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = await File.ReadAllTextAsync(
                "prompts/SelectCategorySchema.md",
                cancellationToken);
            var prompt = $"Tên: {product.Name}\nDanh mục nguồn: {product.Category}";

            return await _geminiService.CategorySchemeForGeminiAsync(
                schemas.Select(schema => schema.Category).ToArray(),
                prompt,
                systemPrompt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to select category schema using Gemini.");
            return Result<GeminiResponse<string>>.Failure(500, "Failed to select category schema.");
        }
    }

    private async Task<Result<CategoryValueSelectionResult>> SelectCategoryValuesAsync(
        Product product,
        CategoryAttributeSchema schema,
        string semanticSearchText,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = await File.ReadAllTextAsync(
                "prompts/CategoryValueSelection.md",
                cancellationToken);
            var prompt = JsonSerializer.Serialize(new
            {
                product = new
                {
                    product.Name,
                    SourceCategory = product.Category,
                    product.Description,
                    product.Brand,
                    product.Metadata,
                    SemanticSearchText = semanticSearchText
                },
                schema = new
                {
                    schema.Category,
                    Attributes = schema.Attributes
                        .Where(attribute => attribute.IsFilterable)
                        .Select(attribute => new
                        {
                            attribute.Key,
                            DataType = attribute.DataType.ToString(),
                            attribute.AllowedValues
                        })
                }
            });

            return await _kernelChatService.SelectCategoryValuesAsync(
                prompt,
                systemPrompt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to select values for category {Category}.", schema.Category);
            return Result<CategoryValueSelectionResult>.Failure(500, "Failed to select category values.");
        }
    }

    private Dictionary<string, Value> BuildCategoryPayload(
        Product product,
        CategoryAttributeSchema schema,
        CategoryValueSelectionResult selection)
    {
        var payload = new Dictionary<string, Value>(StringComparer.Ordinal);
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selectedValue in selection.Values)
        {
            if (string.IsNullOrWhiteSpace(selectedValue.Key)
                || string.IsNullOrWhiteSpace(selectedValue.Value))
            {
                continue;
            }

            if (!usedKeys.Add(selectedValue.Key))
            {
                continue;
            }

            var definition = schema.Attributes.FirstOrDefault(attribute =>
                attribute.IsFilterable
                && string.Equals(attribute.Key, selectedValue.Key, StringComparison.OrdinalIgnoreCase));

            if (definition is null || ReservedPayloadNames.Contains(definition.Key))
            {
                continue;
            }

            var value = selectedValue.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            switch (definition.DataType)
            {
                case AttributeDataType.Keyword:
                    var canonicalValue = definition.AllowedValues?.FirstOrDefault(allowedValue =>
                        string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase));

                    if (definition.AllowedValues is { Count: > 0 } && canonicalValue is null)
                    {
                        _logger.LogWarning(
                            "Kernel returned unsupported value {Value} for category attribute {AttributeKey}.",
                            value,
                            definition.Key);
                        continue;
                    }

                    payload[definition.Key] = canonicalValue ?? value;
                    break;

                case AttributeDataType.Number:
                    if (double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number)
                        && double.IsFinite(number))
                    {
                        payload[definition.Key] = number;
                    }
                    break;

                case AttributeDataType.Boolean:
                    if (bool.TryParse(value, out var boolean))
                    {
                        payload[definition.Key] = boolean;
                    }
                    break;

                default:
                    _logger.LogWarning(
                        "Unsupported data type {DataType} for category attribute {AttributeKey}.",
                        definition.DataType,
                        definition.Key);
                    break;
            }
        }

        // Variant options in Metadata identify the SKU. Shared descriptions can list
        // other colors or even contradict the variant, so they cannot override it.
        foreach (var definition in schema.Attributes.Where(attribute => attribute.IsFilterable))
        {
            if (!ProductVariantAttributes.TryGetCanonicalKeyword(product, definition, out var canonical))
                continue;

            if (canonical is null)
            {
                payload.Remove(definition.Key);
                _logger.LogWarning(
                    "Variant metadata value for {ProductId}/{AttributeKey} is outside the active schema; omitting this filterable payload field.",
                    product.Id, definition.Key);
            }
            else
            {
                payload[definition.Key] = canonical;
            }
        }

        return payload;
    }

    private async Task<Result<GeminiResponse<string>>> BuildSemanticSearchText(
        string embeddingText,
        CancellationToken cancellationToken)
    {
        var systemPrompt = await File.ReadAllTextAsync(
            "prompts/SemanticEmbedding.md",
            cancellationToken);

        try
        {
            var response = await _geminiService.GenerateTextAsyncV2(new GeminiRequest
            {
                Prompt = embeddingText,
                GenerationConfig = new GenerationConfig
                {
                    MaxOutputTokens = 600,
                    Temperature = 0.2
                },
                SystemPrompt = systemPrompt
            }, cancellationToken);

            if (response.IsSuccess && response.Data is not null)
            {
                return Result<GeminiResponse<string>>.Success(response.Data);
            }

            throw new InvalidOperationException(response.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate semantic search text using Gemini.");
            return Result<GeminiResponse<string>>.Failure(
                500,
                "Failed to generate semantic search text using GeminiService.");
        }
    }

    private static Vector ToQdrantDenseVector(IEnumerable<double> values)
    {
        var denseVector = new DenseVector();
        denseVector.Data.Add(values.Select(value => (float)value));

        return new Vector
        {
            Dense = denseVector
        };
    }

    private static readonly HashSet<string> ReservedPayloadNames = new(
        [
            ProductPayloadNames.ProductId,
            ProductPayloadNames.BusinessId,
            ProductPayloadNames.Price,
            ProductPayloadNames.Status,
            ProductPayloadNames.ExternalId,
            ProductPayloadNames.Name,
            ProductPayloadNames.Category
        ],
        StringComparer.OrdinalIgnoreCase);
}
