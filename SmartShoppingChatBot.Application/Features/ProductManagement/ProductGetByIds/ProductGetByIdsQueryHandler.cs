using AutoMapper;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;

public sealed class ProductGetByIdsQueryHandler
    : IRequestHandler<ProductGetByIdsQuery, Result<List<ProductResponseV2>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public ProductGetByIdsQueryHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IMapper mapper)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<Result<List<ProductResponseV2>>> Handle(
        ProductGetByIdsQuery request,
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

        var orderedIds = new List<ObjectId>();
        var seenIds = new HashSet<ObjectId>();

        foreach (var rawProductId in request.ProductIds)
        {
            if (!ObjectId.TryParse(rawProductId?.Trim(), out var productId))
            {
                return Result<List<ProductResponseV2>>.Failure(
                    400,
                    "One or more product IDs are invalid.",
                    messageCode: ProductMessageCode.InvalidId);
            }

            if (seenIds.Add(productId))
            {
                orderedIds.Add(productId);
            }
        }

        var businessId = businessResult.Data.Id;
        var products = await _productRepository.FindAllAsync(product =>
            orderedIds.Contains(product.Id)
            && product.BusinessId == businessId
            && product.Status == ProductStatus.Active);

        var productById = products.ToDictionary(product => product.Id);
        var orderedProducts = orderedIds
            .Where(productById.ContainsKey)
            .Select(productId => productById[productId])
            .ToList();

        var responses = _mapper.Map<List<ProductResponseV2>>(orderedProducts);

        return Result<List<ProductResponseV2>>.Success(
            responses,
            200,
            responses.Count == 0
                ? "No matching products found."
                : "Products retrieved successfully.",
            ProductMessageCode.Success);
    }
}
