using System.ComponentModel;
using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductVariants;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Plugins
{
    public class ProductPluginV2
    {
        private readonly IMediator _mediator;
        private readonly IProductReferenceCollectorV2 _productReferenceCollector;
        private readonly ILogger<ProductPluginV2> _logger;
        // Plugin is scoped to one chat request. Bound retries independently of model instructions.
        private int _semanticSearchCalls;
        private int _categoryBrowseCalls;
        private int _searchCalls;
        private int _searchCallLimit = 2;
        private bool _searchRecoveryRequested;
        private ProductSemanticSearchV2Request? _lastSemanticRequest;
        private readonly Dictionary<string, ProductResponseV2> _productsLoadedById =
            new(StringComparer.OrdinalIgnoreCase);
        public int SemanticSearchCallCount => _semanticSearchCalls;

        public ProductPluginV2(
            IMediator mediator,
            IProductReferenceCollectorV2 productReferenceCollector,
            ILogger<ProductPluginV2> logger)
        {
            _mediator = mediator;
            _productReferenceCollector = productReferenceCollector;
            _logger = logger;
        }

        [KernelFunction]
        [Description(
            "Tìm sản phẩm theo thứ tự bắt buộc: lọc category và key-value trước; không có kết quả phù hợp thì tìm bằng hai vector; cuối cùng mới dùng BM25. " +
            "Đây là function chính cho tìm sản phẩm mới; server tự chuyển bước khi bước trước không có sản phẩm hiện hành khớp bộ lọc hoặc tên loại yêu cầu. " +
            "Nếu xác định được danh mục, gọi Category.GetCategorySchemas trước. Truyền Category chính xác và chỉ truyền Attributes có IsFilterable=true với Value đúng AllowedValues; " +
            "Bước đầu áp dụng category, attributes, giá và ExcludeProductIds. Bước vector và BM25 bỏ category và các Attributes có IsPreference=true (trừ schema IsStrict=true); giữ điều kiện bắt buộc, giá và ExcludeProductIds. " +
            "Phong cách/hoàn cảnh như đi tiệc, sang trọng, quý phái phải nằm trong SemanticQuery và RequiresSemanticMatch=true; nếu đưa vào Attributes thì đánh dấu IsPreference=true. Không suy ra màu, giới tính, chất liệu hoặc giá cao từ các từ này. " +
            "Không tìm được danh mục/schema phù hợp thì để Category rỗng, Attributes=[] và truyền đầy đủ yêu cầu trong các query để bắt đầu từ vector rồi BM25. " +
            "SemanticQuery là nhu cầu tự nhiên đầy đủ; TechnicalQuery là mô tả catalogue ngắn; Bm25Query là cụm từ khóa tên/loại/thương hiệu. " +
            "Không chèn giá vào ba query; dùng PriceBand hoặc MinPrice/MaxPrice. " +
            "ExcludeProductIds chỉ nhận canonical productId đã hiển thị, không nhận externalProductId.")]
        [return: Description(
            "Danh sách sản phẩm của bước category, vector hoặc BM25 sau khi kiểm tra dữ liệu hiện tại và bộ lọc bắt buộc. " +
            "Data chỉ là ứng viên. Chỉ hiển thị sản phẩm có dữ liệu phù hợp với cả loại và mục đích/phong cách khách cần. " +
            "Nếu không chọn được sản phẩm phù hợp, gọi ReviewProductSearch trước khi kết luận không tìm thấy. Không tự gọi vòng lặp giữa các function.")]
        public async Task<Result<List<ProductReferenceV2>>> SemanticProductSearch(
            [Description("Ba query tìm kiếm, category/schema filter, giá và canonical productId cần loại trừ")]
            ProductSemanticSearchV2Request request,
            [Description("Field này không được gọi")]
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(400, "Thiếu yêu cầu tìm kiếm sản phẩm.");
            }
            if (Interlocked.Increment(ref _searchCalls) > _searchCallLimit)
                return Result<List<ProductReferenceV2>>.Failure(429,
                    "Đã hết lượt tìm sản phẩm trong tin nhắn hiện tại. Không gọi lại function tìm kiếm.");
            Interlocked.Increment(ref _semanticSearchCalls);

            request = new ProductSemanticSearchV2Request
            {
                SemanticQuery = request.SemanticQuery?.Trim() ?? string.Empty,
                RequiresSemanticMatch = request.RequiresSemanticMatch,
                TechnicalQuery = request.TechnicalQuery?.Trim() ?? string.Empty,
                Bm25Query = request.Bm25Query?.Trim() ?? string.Empty,
                Category = request.Category?.Trim() ?? string.Empty,
                Attributes = request.Attributes ?? [],
                PriceBand = request.PriceBand,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice,
                ExcludeProductIds = request.ExcludeProductIds ?? []
            };
            _lastSemanticRequest = request;

            _logger.LogInformation(
                "Product.SemanticProductSearch V2 invoked. Category: {Category}, AttributeCount: {AttributeCount}, ExcludedProductCount: {ExcludedProductCount}",
                request.Category,
                request.Attributes.Count,
                request.ExcludeProductIds.Count);

            var result = await _mediator.Send(
                new ProductSemanticSearchV2Query
                {
                    Request = request,
                    IncludeBm25Candidates = _searchRecoveryRequested
                },
                cancellationToken);
            if (result.IsSuccess)
            {
                _productReferenceCollector.AddRangeFromV2(result.Data ?? []);
            }

            return result;
        }

        [KernelFunction]
        [Description(
            "Function kiểm tra lại CHỈ lọc category/key-value, không tìm vector hay BM25. " +
            "Không dùng làm điểm vào tìm kiếm. Chỉ gọi khi ReviewProductSearch chỉ thị để kiểm tra kết quả mà truy vấn tên/ngữ nghĩa có thể bỏ sót. " +
            "Hỗ trợ kết hợp danh mục, màu sắc, kích cỡ, chất liệu và giá trong cùng một lần gọi. " +
            "Trước khi lọc, gọi Category.GetCategorySchemas cho danh mục cần tìm. " +
            "Attributes.Name phải là Key có IsFilterable=true, Value phải sao chép chính xác AllowedValues của thuộc tính đó. " +
            "Nếu AllowedValues rỗng, Value phải đúng DataType và nhu cầu khách đã nói rõ. " +
            "Ví dụ khách nói trắng nhưng AllowedValues có white thì truyền white, không truyền trắng hoặc mau_trang. " +
            "Không có giá trị tương ứng thì không bỏ bộ lọc và không tự tạo giá trị; hỏi khách làm rõ. " +
            "Không nói giá thì PriceBand=Any, MinPrice và MaxPrice=null. " +
            "Khách chỉ nói loại sản phẩm thì Attributes=[]. " +
            "Khách muốn mẫu khác thì giữ bộ lọc được tham chiếu và điền ID đã hiển thị vào ExcludeProductIds; " +
            "các trường hợp khác dùng ExcludeProductIds=[]. " +
            "Sau khi kiểm tra ứng viên, nếu vẫn không phù hợp gọi ReviewProductSearch để nhận bước tiếp theo; không tự gọi lại.")]
        [return: Description(
            "Data là ứng viên lọc chính xác, vẫn phải kiểm tra đúng loại và các điều kiện hiện tại; không bắt buộc hiển thị mọi sản phẩm. " +
            "IsSuccess=true và Data=[]: tra cứu thành công nhưng không có sản phẩm khớp; không phải lỗi. " +
            "IsSuccess=false: đọc Message và Errors để biết lỗi, không kết luận hết hàng.")]
        public async Task<Result<List<ProductReferenceV2>>> BrowseProductsByCategory(
            [Description("Danh mục, thuộc tính, phân khúc giá và các sản phẩm cần loại trừ")]
            CategorySemanticSearchRequest request,
            [Description("Field này không được gọi")]
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Product.BrowseProductsByCategory invoked.");

            if (request is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(400, "Thiếu bộ lọc tra cứu sản phẩm.");
            }
            if (!_searchRecoveryRequested)
                return Result<List<ProductReferenceV2>>.Failure(400,
                    "Dùng SemanticProductSearch trước; chỉ Browse khi ReviewProductSearch yêu cầu kiểm tra lại.");
            if (Interlocked.Increment(ref _searchCalls) > _searchCallLimit)
                return Result<List<ProductReferenceV2>>.Failure(429,
                    "Đã hết lượt tìm sản phẩm trong tin nhắn hiện tại. Không gọi lại function tìm kiếm.");
            Interlocked.Increment(ref _categoryBrowseCalls);

            request = new CategorySemanticSearchRequest
            {
                Category = request.Category?.Trim() ?? string.Empty,
                Bm25Query = null,
                Attributes = request.Attributes ?? [],
                PriceBand = request.PriceBand,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice,
                ExcludeProductIds = request.ExcludeProductIds ?? []
            };

            _logger.LogInformation(
                "Product.BrowseProductsByCategory filters. Category: {Category}, PriceBand: {PriceBand}, AttributeCount: {AttributeCount}, ExcludedProductCount: {ExcludedProductCount}",
                request.Category,
                request.PriceBand,
                request.Attributes.Count,
                request.ExcludeProductIds.Count);

            var result = await _mediator.Send(
                new CategorySemanticSearchQuery { Request = request },
                cancellationToken);

            _logger.LogInformation(
                "Product.BrowseProductsByCategory completed. IsSuccess: {IsSuccess}, StatusCode: {StatusCode}, ProductCount: {ProductCount}, Message: {Message}",
                result.IsSuccess,
                result.StatusCode,
                result.Data?.Count ?? 0,
                result.Message);

            if (result.IsSuccess)
            {
                _productReferenceCollector.AddRangeFromV2(result.Data ?? []);
            }

            return result;
        }

        [KernelFunction]
        [Description("Kiểm tra trước khi kết luận không tìm thấy. Chỉ thị tối đa một lần tìm lại bằng SemanticProductSearch; bước này bao gồm category, vector và BM25. Nếu đã có sản phẩm phù hợp thì trả lời ngay.")]
        public ProductSearchReview ReviewProductSearch(
            [Description("Lý do không chọn được ứng viên; nêu điều kiện hiện tại nào không khớp và dữ liệu chứng minh. Không tự thêm điều kiện từ lịch sử.")]
            string rejectionReason)
        {
            if (!_searchRecoveryRequested)
            {
                _searchCallLimit = Math.Min(_searchCalls + 1, 2);
                _searchRecoveryRequested = true;
            }
            var nextFunctions = new List<string>();
            if (_searchCalls < _searchCallLimit)
                nextFunctions.Add("ProductAndCategory.SemanticProductSearch");
            _logger.LogInformation(
                "Product search review: reason={Reason}, semanticCalls={SemanticCalls}, browseCalls={BrowseCalls}, next={Next}",
                rejectionReason, _semanticSearchCalls, _categoryBrowseCalls, string.Join(", ", nextFunctions));
            return new ProductSearchReview
            {
                NextFunctions = nextFunctions,
                CanConclude = nextFunctions.Count == 0,
                Instruction = nextFunctions.Count == 0
                    ? "Đã hết bước kiểm tra lại. Chỉ hiển thị ứng viên thỏa nhu cầu hiện tại. Nếu không có, trả chưa tìm thấy và selectedProductIds=[]. Nếu tool báo lỗi dịch vụ, báo chưa tra cứu được, không khẳng định không có hàng."
                    : "Chỉ gọi function trong NextFunctions một lần rồi đánh giá dữ liệu mới. Có sản phẩm đúng thì trả lời ngay. Không tìm lại lần thứ hai. " +
                      "Dựng lại nhu cầu từ tin nhắn hiện tại: thay điều kiện cũ bị thay đổi; dùng đúng schema/AllowedValues. " +
                      "Chỉ nói phân khúc giá thì MinPrice/MaxPrice=null; không yêu cầu mẫu khác thì ExcludeProductIds=[]. " +
                      "Bm25Query chỉ chứa loại/tên/brand, không chứa màu hoặc giá đã có filter. Không xóa điều kiện bắt buộc để có hàng. " +
                      "Chỉ kiểm tra mục đích/phong cách khi khách thực sự yêu cầu; không loại áo đúng màu và giá vì thiếu phong cách không được hỏi."
            };
        }

        // Called by the chat service when the model tries to finish a product search without a product.
        // Review and one follow-up search run on the server, regardless of model tool choice.
        public async Task<ProductSearchRecovery> ReviewAndRetryAsync(CancellationToken cancellationToken = default)
        {
            var request = _lastSemanticRequest;
            var review = ReviewProductSearch(
                "Câu trả lời dự kiến không chọn sản phẩm; kiểm tra lại trước khi kết luận.");
            if (request is null)
                return new ProductSearchRecovery
                {
                    Review = review,
                    SearchWasCalled = false
                };

            var recovery = new ProductSearchRecovery
            {
                Review = review,
                SearchWasCalled = true
            };
            if (_searchCalls < _searchCallLimit)
            {
                var semantic = await SemanticProductSearch(request, cancellationToken);
                recovery.Steps.Add(new ProductSearchRecoveryStep
                {
                    Function = "SemanticProductSearch",
                    IsSuccess = semantic.IsSuccess,
                    Message = semantic.Message ?? string.Empty,
                    Products = semantic.Data ?? []
                });
            }

            return recovery;
        }

        [KernelFunction]
        [Description(
            "Lấy dữ liệu hiện tại của nhiều sản phẩm bằng externalProductId từ hệ thống bán hàng, ví dụ ID dùng trong link thêm vào giỏ. " +
            "Chỉ dùng khi đầu vào thực sự là externalProductId; để tải theo canonical productId phải dùng function tương ứng khác. " +
            "Kết quả giữ thứ tự danh sách đầu vào, gồm QdrantPayload với category/thuộc tính canonical, " +
            "và chỉ gồm sản phẩm đang hoạt động của business hiện tại.")]
        public async Task<Result<List<ProductReferenceV2>>> GetProductsByExternalIds(
            [Description("Danh sách externalProductId cần tải")]
            ProductGetByExternalIdsRequest request,
            [Description("Field này không được gọi")]
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return Result<List<ProductReferenceV2>>.Failure(400, "Thiếu danh sách externalProductId.");
            }

            request = new ProductGetByExternalIdsRequest
            {
                ExternalProductIds = request.ExternalProductIds ?? []
            };
            var result = await _mediator.Send(
                new ProductGetByExternalIdsQuery { Request = request },
                cancellationToken);
            if (result.IsSuccess)
            {
                _productReferenceCollector.AddRangeFromV2(result.Data ?? []);
            }

            return result;
        }

        [KernelFunction]
        [Description("Lấy dữ liệu mới nhất của sản phẩm theo canonical productId từ productReferences, gồm QdrantPayload với category và thuộc tính canonical. Khi khách hỏi biến thể cùng mẫu, AI phải gọi function này trước FindSimilarProductVariants và dùng productId của sản phẩm tìm được. Không truyền externalProductId.")]
        public async Task<Result<List<ProductResponseV2>>> GetProductsByIds(
            [Description("Danh sách canonical productId cần lấy từ database")]
            ProductByIdsRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request?.ProductIds is not { Count: > 0 })
                return Result<List<ProductResponseV2>>.Failure(400, "Thiếu canonical productId.");

            var result = await _mediator.Send(
                new ProductGetByIdsQuery { ProductIds = request.ProductIds }, cancellationToken);
            if (result.IsSuccess)
            {
                foreach (var product in result.Data ?? [])
                    _productsLoadedById[product.ProductId] = product.Copy();

                _productReferenceCollector.AddRangeFromV2((result.Data ?? []).Select(product =>
                    new ProductReferenceV2
                    {
                        ProductId = product.ProductId,
                        ExternalProductId = product.ExternalProductId,
                        HasCurrentData = true,
                        Name = product.Name,
                        Description = product.Description,
                        ExternalProductUrl = product.ExternalProductUrl,
                        Price = decimal.TryParse(product.Price, NumberStyles.Number,
                            CultureInfo.InvariantCulture, out var price) ? price : null,
                        Brand = product.Brand,
                        Category = product.Category,
                        StockQuantity = product.StockQuantity,
                        Images = product.Images.ToList(),
                        Metadata = new Dictionary<string, string>(product.Metadata),
                        QdrantPayload = new Dictionary<string, string>(product.QdrantPayload)
                    }));
            }
            return result;
        }

        [KernelFunction]
        [Description(
            "Tìm SKU tương tự của một sản phẩm đã hiển thị nhưng khác một hoặc nhiều thuộc tính, ví dụ size khác, màu khác, hoặc màu cụ thể. " +
            "Chỉ gọi SAU KHI AI đã gọi GetProductsByIds trong lượt hiện tại để lấy dữ liệu mới nhất của sản phẩm gốc. " +
            "Function này không tự gọi GetProductsByIds; nếu chưa tải ID đó, sẽ trả lỗi yêu cầu gọi GetProductsByIds trước. " +
            "Dùng QdrantPayload.category của sản phẩm gốc để chọn schema, không dùng Category nguồn từ MongoDB. " +
            "Chỉ tìm cùng tên mẫu, brand, category và giữ các thuộc tính filterable canonical không đổi. " +
            "ChangedAttributes.Name phải là Key filterable từ Category.GetCategorySchemas; Value là AllowedValues chính xác nếu khách nêu giá trị đích, hoặc null nếu chỉ hỏi giá trị khác. " +
            "Giữ PriceBand/MinPrice/MaxPrice của nhu cầu đang được tham chiếu. Không dùng cho yêu cầu mẫu hoàn toàn khác.")]
        public async Task<Result<List<ProductReferenceV2>>> FindSimilarProductVariants(
            [Description("Canonical productId, thuộc tính muốn thay đổi và khoảng giá cần giữ")]
            ProductVariantSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.ProductId))
                return Result<List<ProductReferenceV2>>.Failure(400, "Thiếu canonical productId của sản phẩm gốc.");

            _logger.LogInformation("Product.FindSimilarProductVariants invoked for {ProductId}", request.ProductId);
            if (!_productsLoadedById.TryGetValue(request.ProductId.Trim(), out var source))
                return Result<List<ProductReferenceV2>>.Failure(400,
                    "Chưa tải dữ liệu hiện tại của sản phẩm gốc. Hãy gọi ProductAndCategory.GetProductsByIds với canonical productId này trước, rồi gọi lại FindSimilarProductVariants.");

            var result = await _mediator.Send(new ProductVariantSearchQuery(source.Copy(), request), cancellationToken);
            if (result.IsSuccess) _productReferenceCollector.AddRangeFromV2(result.Data ?? []);
            return result;
        }

    }
}
