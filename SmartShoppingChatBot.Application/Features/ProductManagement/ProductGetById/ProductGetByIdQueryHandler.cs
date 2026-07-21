using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetById;

public class ProductGetByIdQueryHandler : IRequestHandler<ProductGetByIdQuery, Result<ProductResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;

    public ProductGetByIdQueryHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
    }

    public async Task<Result<ProductResponse>> Handle(
        ProductGetByIdQuery request,
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

        var businessId = businessResult.Data.Id;
        var product = request.ProductId.HasValue
            ? await _productRepository.FindAsync(product =>
                product.Id == request.ProductId.Value
                && product.BusinessId == businessId
                && product.Status != ProductStatus.Deleted)
            : await _productRepository.FindAsync(product =>
                product.ExternalId == request.ExternalId
                && product.BusinessId == businessId
                && product.Status != ProductStatus.Deleted);

        if (product == null)
        {
            return Result<ProductResponse>.Failure(
                404,
                "Product not found.",
                messageCode: ProductMessageCode.NotFound);
        }

        return Result<ProductResponse>.Success(
            ProductMappings.ToResponse(product),
            200,
            "Product retrieved successfully.",
            ProductMessageCode.Success);
    }
}
