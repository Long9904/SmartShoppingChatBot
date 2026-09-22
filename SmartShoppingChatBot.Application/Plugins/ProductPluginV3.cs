namespace SmartShoppingChatBot.Application.Plugins
{
    using System.ComponentModel;
    using Microsoft.SemanticKernel;
    using SmartShoppingChatBot.Application.Commons.Results;
    using SmartShoppingChatBot.Application.DTOs;
    using SmartShoppingChatBot.Application.Interface;

    // The plugin exposes tool descriptions; product rules live in the search service.
    public sealed class ProductPluginV3(
        IProductSemanticSearchByAI search,
        IProductReferenceCollectorV3 collector)
    {
        [KernelFunction]
        [Description("Find products using needs, an exact supplied category, allowed attributes, and a price band or numeric budget. Use this for cheap/expensive products without a reference item. Set Sort only when the customer explicitly asks for the cheapest or most expensive matching products. Never calculate business price limits yourself.")]
        public async Task<Result<List<ProductReferenceV3>>> SemanticProductSearch(
            ProductSearchRequestV3 request, CancellationToken cancellationToken = default)
        {
            var result = await search.SearchAsync(request, cancellationToken);
            collector.AddRange(result.Data ?? []);
            return result;
        }

        [KernelFunction]
        [Description("Find cheaper or more expensive replacements for a known canonical productId. Keep the original product type and user requirements. The server reads the current price, applies the existing relative range, and excludes the reference product.")]
        public async Task<Result<List<ProductReferenceV3>>> SearchPriceAlternatives(
            ProductPriceAlternativeRequestV3 request, CancellationToken cancellationToken = default)
        {
            var result = await search.SearchPriceAlternativesAsync(request, cancellationToken);
            collector.AddRange(result.Data ?? []);
            return result;
        }

        [KernelFunction]
        [Description("Find complementary products or items to style with a known productId. For a general styling request choose up to three target categories from the supplied list. For an explicit request such as shoes choose only that type. Describe target products in each query. Results are grouped; show only Products from successful groups. Empty or failed groups do not invalidate other groups.")]
        public async Task<Result<List<ProductCrossSellGroupV3>>> SearchComplementaryProducts(
            ProductCrossSellRequestV3 request, CancellationToken cancellationToken = default)
        {
            var result = await search.CrossSellAsync(request, cancellationToken);
            collector.AddRange((result.Data ?? []).Where(group => group.IsSuccess).SelectMany(group => group.Products));
            return result;
        }

        [KernelFunction]
        [Description("Load current product facts for canonical productIds already in the conversation. Use for details, comparisons, or refreshing source attributes before styling. Never use names or externalProductId as canonical IDs.")]
        public async Task<Result<List<ProductReferenceV3>>> GetProductsByIds(
            ProductByIdsRequestV3 request, CancellationToken cancellationToken = default)
        {
            var result = await search.GetByIdsAsync(request, cancellationToken);
            collector.AddRange(result.Data ?? []);
            return result;
        }
    }
}
