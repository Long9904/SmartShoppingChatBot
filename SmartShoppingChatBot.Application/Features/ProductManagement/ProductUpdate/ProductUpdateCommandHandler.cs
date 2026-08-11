using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductUpdate;

public class ProductUpdateCommandHandler : IRequestHandler<ProductUpdateCommand, Result<ProductResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQdrantService _qdrantService;
    private readonly IBusinessQuotaRepository _businessQuotaRepository;
    private readonly IPublishEndpoint _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProductUpdateCommandHandler> _logger;

    public ProductUpdateCommandHandler(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IQdrantService qdrantService,
        IBusinessQuotaRepository businessQuotaRepository,
        IPublishEndpoint publisher,
        TimeProvider timeProvider,
        ILogger<ProductUpdateCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _qdrantService = qdrantService;
        _businessQuotaRepository = businessQuotaRepository;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<ProductResponse>> Handle(
        ProductUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data == null)
        {
            return Result<ProductResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var business = businessResult.Data;
        var product = request.ProductId.HasValue
            ? await _productRepository.FindAsync(product =>
                product.Id == request.ProductId.Value
                && product.BusinessId == business.Id
                && product.Status != ProductStatus.Deleted)
            : await _productRepository.FindAsync(product =>
                product.ExternalId == request.LookupExternalId
                && product.BusinessId == business.Id
                && product.Status != ProductStatus.Deleted);

        if (product == null)
        {
            return Result<ProductResponse>.Failure(
                404,
                "Product not found.",
                messageCode: ProductMessageCode.NotFound);
        }

        if (!string.Equals(product.ExternalId, request.ExternalId, StringComparison.Ordinal))
        {
            var conflictingProduct = await _productRepository.FindAsync(other =>
                other.BusinessId == business.Id
                && other.ExternalId == request.ExternalId
                && other.Id != product.Id
                && other.Status != ProductStatus.Deleted);

            if (conflictingProduct != null)
            {
                return Result<ProductResponse>.Failure(
                    409,
                    "ExternalId already exists.",
                    messageCode: ProductMessageCode.ExternalIdConflict);
            }
        }

        var actorResult = await ProductActorResolver.ResolveAsync(
            _currentUserService,
            _httpContextAccessor,
            business);

        if (!actorResult.IsSuccess || actorResult.Data == null)
        {
            return Result<ProductResponse>.Failure(
                actorResult.StatusCode,
                actorResult.Message,
                actorResult.Errors,
                actorResult.MessageCode);
        }

        var requiresReembedding = RequiresReembedding(product, request);
        var hadQdrantPoint = product.Status != ProductStatus.PendingEmbedding;

        if (requiresReembedding)
        {
            var businessQuota = await _businessQuotaRepository
                .GetCurrentBusinessQuota(business.Id);

            if (businessQuota == null)
            {
                return Result<ProductResponse>.Failure(
                    404,
                    "Business quota not found.",
                    messageCode: BusinessQuotaMessageCode.NotFound);
            }

            var remainingTokens = businessQuota.TokenLimit - businessQuota.UsedTokens;

            if (remainingTokens < ProductEmbeddingQuota.TokenBudgetPerProduct)
            {
                return Result<ProductResponse>.Failure(
                    429,
                    "Not enough token quota to re-embed product.",
                    messageCode: BusinessQuotaMessageCode.TokenLimitExceeded);
            }
        }

        product.ExternalId = request.ExternalId;
        product.ExternalProductUrl = request.ExternalProductUrl ?? string.Empty;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Currency = request.Currency;
        product.Brand = request.Brand;
        product.StockQuantity = request.StockQuantity;
        product.Category = request.Category;
        product.Images = request.Images;
        product.Metadata = request.Metadata;
        product.UpdatedAt = _timeProvider.GetUtcNow();
        product.UpdatedBy = actorResult.Data;

        if (requiresReembedding)
        {
            product.SearchContent = product.BuildEmbeddingText();
            product.Status = ProductStatus.PendingEmbedding;
        }

        try
        {
            if (!requiresReembedding && hadQdrantPoint)
            {
                await _qdrantService.SetPayloadAsync(
                    QdrantCollections.Products,
                    [product.QdrantPointId],
                    ProductMappings.BuildQdrantPayload(product),
                    cancellationToken);
            }

            await _productRepository.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (requiresReembedding)
            {
                await _publisher.Publish(new ProductCreateEvent
                {
                    ProductId = product.Id.ToString(),
                    QdrantPointId = product.QdrantPointId
                }, cancellationToken);
            }

            return Result<ProductResponse>.Success(
                ProductMappings.ToResponse(product),
                200,
                requiresReembedding
                    ? "Product updated and queued for re-embedding."
                    : "Product updated successfully.",
                ProductMessageCode.UpdateSuccess);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update product {ProductId}.", product.Id);
            return Result<ProductResponse>.Failure(500, "Failed to update product.");
        }
    }

    private static bool RequiresReembedding(
        Product product,
        ProductUpdateCommand request)
    {
        return !string.Equals(product.Name, request.Name, StringComparison.Ordinal)
            || !string.Equals(product.Description, request.Description, StringComparison.Ordinal)
            || !string.Equals(product.Brand, request.Brand, StringComparison.Ordinal)
            || !string.Equals(product.Category, request.Category, StringComparison.Ordinal)
            || !MetadataEquals(product.Metadata, request.Metadata);
    }

    private static bool MetadataEquals(
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> updated)
    {
        return current.Count == updated.Count
            && current.All(item =>
                updated.TryGetValue(item.Key, out var value)
                && string.Equals(item.Value, value, StringComparison.Ordinal));
    }
}
