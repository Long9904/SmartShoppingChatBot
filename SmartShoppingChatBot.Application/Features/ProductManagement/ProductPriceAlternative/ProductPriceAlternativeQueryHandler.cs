using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductPriceAlternative;

public class ProductPriceAlternativeQueryHandler : IRequestHandler<ProductPriceAlternativeQuery, Result<List<ProductResponseV3>>>
{
    private const decimal DownSellMinimumRatio = 0.85m;
    private const decimal UpSellMaximumRatio = 1.20m;

    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly ISender _sender;
    private readonly ILogger<ProductPriceAlternativeQueryHandler> _logger;

    public ProductPriceAlternativeQueryHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        ILogger<ProductPriceAlternativeQueryHandler> logger,
        ISender sender)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<List<ProductResponseV3>>> Handle(
        ProductPriceAlternativeQuery query,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<List<ProductResponseV3>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        if (!ObjectId.TryParse(query.Request.ReferenceProductId.Trim(), out var referenceProductId))
        {
            return Result<List<ProductResponseV3>>.Failure(
                400,
                "Reference product ID is invalid.",
                messageCode: ProductMessageCode.InvalidId);
        }

        var referenceProduct = await _productRepository.FindAsync(product =>
            product.Id == referenceProductId
            && product.BusinessId == businessResult.Data.Id
            && product.Status == ProductStatus.Active);

        if (referenceProduct is null)
        {
            return Result<List<ProductResponseV3>>.Failure(
                404,
                "Reference product not found.",
                messageCode: ProductMessageCode.NotFound);
        }

        if (referenceProduct.Price <= 0)
        {
            return Result<List<ProductResponseV3>>.Failure(
                400,
                "Reference product must have a positive price for relative price search.");
        }

        var priceRange = BuildPriceRange(
            referenceProduct.Price,
            query.Request.Strategy);
        var excludedProductIds = query.Request.ExcludeProductIds
            .Append(referenceProduct.Id.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var additionalRequirements = string.IsNullOrWhiteSpace(query.Request.AdditionalRequirements)
            ? string.Empty
            : $" Yêu cầu bổ sung: {query.Request.AdditionalRequirements.Trim()}.";

        var searchRequest = new ProductSemanticSearchRequest
        {
            SemanticQuery = query.Request.SemanticQuery.Trim() + additionalRequirements,
            TechnicalQuery = BuildTechnicalQuery(referenceProduct, additionalRequirements),
            MinPrice = priceRange.MinPrice,
            MaxPrice = priceRange.MaxPrice,
            ExcludeProductIds = excludedProductIds
        };
        _logger.LogInformation("ProductPriceAlternative is calling");
        return await _sender.Send(
            new ProductSemanticSearchQuery { Request = searchRequest },
            cancellationToken);
    }

    private static (decimal? MinPrice, decimal? MaxPrice) BuildPriceRange(
        decimal referencePrice,
        PriceAlternativeStrategy strategy)
    {
        const decimal priceStep = 0.01m;

        return strategy switch
        {
            PriceAlternativeStrategy.DownSell => (
                referencePrice * DownSellMinimumRatio,
                Math.Max(0m, referencePrice - priceStep)),
            PriceAlternativeStrategy.UpSell => (
                referencePrice + priceStep,
                referencePrice * UpSellMaximumRatio),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported price strategy.")
        };
    }

    private static string BuildTechnicalQuery(Product referenceProduct, string additionalRequirements)
    {
        var brand = string.IsNullOrWhiteSpace(referenceProduct.Brand)
            ? string.Empty
            : $" Thương hiệu: {referenceProduct.Brand}.";

        return $"Loại sản phẩm: {referenceProduct.Category}. " +
               $"Sản phẩm tương tự: {referenceProduct.Name}." +
               brand +
               additionalRequirements;
    }
}
