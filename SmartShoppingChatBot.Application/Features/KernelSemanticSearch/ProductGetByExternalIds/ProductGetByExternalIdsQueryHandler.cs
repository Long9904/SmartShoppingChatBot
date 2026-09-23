using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;

public sealed class ProductGetByExternalIdsQueryHandler(
    ICurrentUserService currentUserService,
    IProductRepository productRepository,
    IQdrantService qdrantService)
    : IRequestHandler<ProductGetByExternalIdsQuery, Result<List<ProductReferenceV2>>>
{
    public async Task<Result<List<ProductReferenceV2>>> Handle(
        ProductGetByExternalIdsQuery query,
        CancellationToken cancellationToken)
    {
        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<List<ProductReferenceV2>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var externalProductIds = query.Request.ExternalProductIds
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var products = await productRepository.FindAllAsync(product =>
            product.BusinessId == businessResult.Data.Id
            && product.Status == ProductStatus.Active
            && externalProductIds.Contains(product.ExternalId));
        var productByExternalId = products
            .GroupBy(product => product.ExternalId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var references = externalProductIds
            .Where(productByExternalId.ContainsKey)
            .Select(externalId => ProductReferenceV2.FromProduct(productByExternalId[externalId]))
            .Select((product, index) =>
            {
                product.DisplayOrder = index + 1;
                return product;
            })
            .ToList();

        foreach (var reference in references)
        {
            if (MongoDB.Bson.ObjectId.TryParse(reference.ProductId, out var productId))
                reference.QdrantPayload = await ProductQdrantPayloadReader.LoadAsync(
                    qdrantService, businessResult.Data.Id, productId, cancellationToken);
        }

        return Result<List<ProductReferenceV2>>.Success(
            references,
            message: references.Count == 0
                ? "Không tìm thấy sản phẩm đang hoạt động theo externalProductId."
                : "Lấy sản phẩm theo externalProductId thành công.");
    }
}
