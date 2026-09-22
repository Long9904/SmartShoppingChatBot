namespace SmartShoppingChatBot.Application.Interface
{
    using SmartShoppingChatBot.Application.Commons.Results;
    using SmartShoppingChatBot.Application.DTOs;

    // This name is the agreed exception to the V3 suffix.
    public interface IProductSemanticSearchByAI
    {
        Task<Result<List<ProductReferenceV3>>> BrowseCategoryAsync(ProductCategoryBrowseRequestV3 request, CancellationToken ct);
        Task<Result<List<ProductReferenceV3>>> SearchAsync(ProductSearchRequestV3 request, CancellationToken ct);
        Task<Result<List<ProductReferenceV3>>> GetByIdsAsync(ProductByIdsRequestV3 request, CancellationToken ct);
        Task<Result<List<ProductReferenceV3>>> SearchPriceAlternativesAsync(ProductPriceAlternativeRequestV3 request, CancellationToken ct);
        Task<Result<List<ProductCrossSellGroupV3>>> CrossSellAsync(ProductCrossSellRequestV3 request, CancellationToken ct);
    }
}
