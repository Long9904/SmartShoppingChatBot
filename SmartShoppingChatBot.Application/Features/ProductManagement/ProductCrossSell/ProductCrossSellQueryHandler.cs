using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCrossSell;

public sealed class ProductCrossSellQueryHandler
    : IRequestHandler<ProductCrossSellQuery, Result<List<ProductResponseV2>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly ISender _sender;

    public ProductCrossSellQueryHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        ISender sender)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _sender = sender;
    }

    public async Task<Result<List<ProductResponseV2>>> Handle(
        ProductCrossSellQuery query,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<List<ProductResponseV2>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        if (!ObjectId.TryParse(query.Request.ReferenceProductId.Trim(), out var referenceProductId))
        {
            return Result<List<ProductResponseV2>>.Failure(
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
            return Result<List<ProductResponseV2>>.Failure(
                404,
                "Reference product not found.",
                messageCode: ProductMessageCode.NotFound);
        }

        var accessoryNeed = string.IsNullOrWhiteSpace(query.Request.AccessoryNeed)
            ? "phụ kiện phù hợp"
            : query.Request.AccessoryNeed.Trim();

        var excludedProductIds = query.Request.ExcludeProductIds
            .Append(referenceProduct.Id.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var searchRequest = new ProductSemanticSearchRequest
        {
            SemanticQuery = query.Request.SemanticQuery.Trim(),
            TechnicalQuery = BuildTechnicalQuery(referenceProduct, accessoryNeed),
            MinPrice = null,
            MaxPrice = query.Request.MaxPrice,
            ExcludeProductIds = excludedProductIds
        };

        return await _sender.Send(
            new ProductSemanticSearchQuery { Request = searchRequest },
            cancellationToken);
    }

    private static string BuildTechnicalQuery(Product referenceProduct, string accessoryNeed)
    {
        var brand = string.IsNullOrWhiteSpace(referenceProduct.Brand)
            ? string.Empty
            : $" Thương hiệu sản phẩm chính: {referenceProduct.Brand}.";

        return $"Loại sản phẩm cần tìm: {accessoryNeed}. " +
               $"Tương thích với sản phẩm: {referenceProduct.Name}. " +
               $"Danh mục sản phẩm chính: {referenceProduct.Category}." +
               brand;
    }
}
