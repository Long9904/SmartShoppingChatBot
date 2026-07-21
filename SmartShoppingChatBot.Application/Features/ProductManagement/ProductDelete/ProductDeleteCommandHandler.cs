using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductDelete;

public class ProductDeleteCommandHandler : IRequestHandler<ProductDeleteCommand, Result<ProductResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQdrantService _qdrantService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProductDeleteCommandHandler> _logger;

    public ProductDeleteCommandHandler(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IQdrantService qdrantService,
        TimeProvider timeProvider,
        ILogger<ProductDeleteCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _qdrantService = qdrantService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<ProductResponse>> Handle(
        ProductDeleteCommand request,
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
                product.ExternalId == request.ExternalId
                && product.BusinessId == business.Id
                && product.Status != ProductStatus.Deleted);

        if (product == null)
        {
            return Result<ProductResponse>.Failure(
                404,
                "Product not found.",
                messageCode: ProductMessageCode.NotFound);
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

        var hadQdrantPoint = product.Status != ProductStatus.PendingEmbedding;
        product.Status = ProductStatus.Deleted;
        product.UpdatedAt = _timeProvider.GetUtcNow();
        product.UpdatedBy = actorResult.Data;

        try
        {
            if (hadQdrantPoint)
            {
                await _qdrantService.SetPayloadAsync(
                    QdrantCollections.Products,
                    [product.QdrantPointId],
                    new Dictionary<string, Value>
                    {
                        [ProductPayloadNames.Status] = ProductStatus.Deleted.ToString(),
                        ["status"] = ProductStatus.Deleted.ToString()
                    },
                    cancellationToken);
            }

            await _productRepository.UpdateAsync(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ProductResponse>.Success(
                ProductMappings.ToResponse(product),
                200,
                "Product deleted successfully.",
                ProductMessageCode.DeleteSuccess);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete product {ProductId}.", product.Id);
            return Result<ProductResponse>.Failure(500, "Failed to delete product.");
        }
    }
}
