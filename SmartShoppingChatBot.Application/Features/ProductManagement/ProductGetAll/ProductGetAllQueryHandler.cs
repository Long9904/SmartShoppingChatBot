using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;

public class ProductGetAllQueryHandler
    : IRequestHandler<ProductGetAllQuery, Result<BasePaginatedList<ProductResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;

    public ProductGetAllQueryHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
    }

    public async Task<Result<BasePaginatedList<ProductResponse>>> Handle(
        ProductGetAllQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data == null)
        {
            return Result<BasePaginatedList<ProductResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var filter = request.Filter;
        var businessId = businessResult.Data.Id;
        var query = _productRepository.AsQueryable()
            .Where(product =>
                product.BusinessId == businessId
                && product.Status != ProductStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(filter.ExternalId))
        {
            query = query.Where(product => product.ExternalId == filter.ExternalId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            query = query.Where(product => product.Name.Contains(filter.Name));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(product => product.Price >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(product => product.Price <= filter.MaxPrice.Value);
        }

        if (filter.MinStockQuantity.HasValue)
        {
            query = query.Where(product => product.StockQuantity >= filter.MinStockQuantity.Value);
        }

        if (filter.MaxStockQuantity.HasValue)
        {
            query = query.Where(product => product.StockQuantity <= filter.MaxStockQuantity.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(product => product.Category.Contains(filter.Category));
        }

        if (filter.Status.HasValue && filter.Status.Value != ProductStatus.Deleted)
        {
            query = query.Where(product => product.Status == filter.Status.Value);
        }
        else if (filter.Status == ProductStatus.Deleted)
        {
            query = query.Where(_ => false);
        }

        query = ApplyOrder(query, filter.OrderBy ?? "UpdatedAt desc");

        var page = await _productRepository.PaginatedListAsync(
            query,
            filter.PageIndex,
            filter.PageSize);

        var response = new BasePaginatedList<ProductResponse>(
            page.Items.Select(ProductMappings.ToResponse).ToList(),
            page.TotalItems,
            page.PageIndex,
            page.PageSize);

        return Result<BasePaginatedList<ProductResponse>>.Success(
            response,
            200,
            "Products retrieved successfully.",
            ProductMessageCode.Success);
    }

    private static IQueryable<Product> ApplyOrder(IQueryable<Product> query, string orderBy)
    {
        return orderBy.Trim().ToLowerInvariant() switch
        {
            "externalid" or "externalid asc" => query.OrderBy(product => product.ExternalId),
            "externalid desc" => query.OrderByDescending(product => product.ExternalId),
            "name" or "name asc" => query.OrderBy(product => product.Name),
            "name desc" => query.OrderByDescending(product => product.Name),
            "price" or "price asc" => query.OrderBy(product => product.Price),
            "price desc" => query.OrderByDescending(product => product.Price),
            "currency" or "currency asc" => query.OrderBy(product => product.Currency),
            "currency desc" => query.OrderByDescending(product => product.Currency),
            "brand" or "brand asc" => query.OrderBy(product => product.Brand),
            "brand desc" => query.OrderByDescending(product => product.Brand),
            "stockquantity" or "stockquantity asc" => query.OrderBy(product => product.StockQuantity),
            "stockquantity desc" => query.OrderByDescending(product => product.StockQuantity),
            "category" or "category asc" => query.OrderBy(product => product.Category),
            "category desc" => query.OrderByDescending(product => product.Category),
            "status" or "status asc" => query.OrderBy(product => product.Status),
            "status desc" => query.OrderByDescending(product => product.Status),
            "createdat" or "createdat asc" => query.OrderBy(product => product.CreatedAt),
            "createdat desc" => query.OrderByDescending(product => product.CreatedAt),
            "updatedat" or "updatedat asc" => query.OrderBy(product => product.UpdatedAt),
            _ => query.OrderByDescending(product => product.UpdatedAt)
        };
    }
}
