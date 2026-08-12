using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreateEmbed;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ProductSemanticSearch
{
    [Fact]
    public async Task Handle_WhenBusinessAuthenticationFails_ReturnsOriginalFailure()
    {
        var fixture = new SemanticSearchFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        fixture.Gemini.Verify(service => service.EmbeddingsAsyncV3(
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenVectorBuildFails_ReturnsOriginalFailureAndStops()
    {
        var fixture = new SemanticSearchFixture();
        fixture.Gemini.Setup(service => service.EmbeddingsAsyncV3(
                It.IsAny<IReadOnlyList<string>>(), "RETRIEVAL_QUERY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<IReadOnlyList<double[]>>>.Failure(503, "Gemini unavailable"));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.StatusCode.Should().Be(503);
        fixture.Qdrant.Verify(service => service.HybridSearchAsync(
            It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlwaysScopesQdrantFilterToBusinessAndActiveProducts()
    {
        var fixture = new SemanticSearchFixture(emptyPoints: true);
        Filter? captured = null;
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<float[], float[], Filter, int, CancellationToken>((_, _, filter, _, _) => captured = filter)
            .ReturnsAsync([]);

        await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Must.Select(condition => condition.Field.Key)
            .Should().Contain([ProductPayloadNames.BusinessId, ProductPayloadNames.Status]);
    }

    [Theory]
    [InlineData(true, false, 3)]
    [InlineData(false, true, 3)]
    [InlineData(true, true, 3)]
    [InlineData(false, false, 2)]
    public async Task Handle_BuildsOptionalPriceRangeOnlyWhenRequested(
        bool hasMinimum, bool hasMaximum, int expectedMustConditions)
    {
        var fixture = new SemanticSearchFixture(emptyPoints: true);
        Filter? captured = null;
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<float[], float[], Filter, int, CancellationToken>((_, _, filter, _, _) => captured = filter)
            .ReturnsAsync([]);

        await fixture.Handler.Handle(fixture.Query(
            hasMinimum ? 100m : null,
            hasMaximum ? 500m : null), CancellationToken.None);

        captured!.Must.Should().HaveCount(expectedMustConditions);
        captured.Must.Count(condition => condition.Field.Key == ProductPayloadNames.Price)
            .Should().Be(expectedMustConditions - 2);
    }

    [Fact]
    public async Task Handle_WhenProductsAreExcluded_AddsQdrantMustNotConditionsAndDoesNotReturnThem()
    {
        var fixture = new SemanticSearchFixture(productCount: 3);
        var excludedIds = new[]
        {
            fixture.Products[0].Id.ToString(),
            fixture.Products[1].Id.ToString()
        };
        Filter? captured = null;
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<float[], float[], Filter, int, CancellationToken>((_, _, filter, _, _) => captured = filter)
            .ReturnsAsync(fixture.Products.Select(product => SemanticSearchFixture.Point(product.Id.ToString())).ToList());

        var result = await fixture.Handler.Handle(
            fixture.Query(excludeProductIds: excludedIds),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.MustNot.Should().HaveCount(2);
        captured.MustNot.Select(condition => condition.Field.Match.Keyword)
            .Should().BeEquivalentTo(excludedIds);
        result.Data.Should().ContainSingle()
            .Which.ProductId.Should().Be(fixture.Products[2].Id.ToString());
    }

    [Fact]
    public async Task Handle_WhenQdrantPointsHaveNoValidProductIds_ReturnsEmptyWithoutRepositoryCall()
    {
        var fixture = new SemanticSearchFixture(emptyPoints: true);
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([SemanticSearchFixture.Point(null), SemanticSearchFixture.Point("not-an-object-id")]);

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("No matching products found.");
        fixture.ProductRepository.Verify(repository => repository.FindAllAsync(
            It.IsAny<Expression<Func<Product, bool>>>(),
            It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeduplicatesQdrantIdsBeforeLoadingProducts()
    {
        var fixture = new SemanticSearchFixture();
        var id = fixture.Products[0].Id.ToString();
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([SemanticSearchFixture.Point(id), SemanticSearchFixture.Point(id)]);
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<RankRecord>, CancellationToken>((_, records, _) => records.Count().Should().Be(1))
            .ReturnsAsync(SemanticSearchFixture.Rerank((id, 0.95f)));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PreservesQdrantOrderBeforeReranking()
    {
        var fixture = new SemanticSearchFixture();
        var first = fixture.Products[0];
        var second = fixture.Products[1];
        fixture.Qdrant.Setup(service => service.HybridSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([SemanticSearchFixture.Point(second.Id.ToString()), SemanticSearchFixture.Point(first.Id.ToString())]);
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<RankRecord>, CancellationToken>((_, records, _) =>
                records.Select(record => record.Id).Should().Equal(second.Id.ToString(), first.Id.ToString()))
            .ReturnsAsync(SemanticSearchFixture.Rerank((first.Id.ToString(), 0.95f)));

        await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        fixture.Gemini.VerifyAll();
    }

    [Fact]
    public async Task Handle_WhenRedisConfigMissing_UsesDefaultTopFiveAndScoreThreshold()
    {
        var fixture = new SemanticSearchFixture(productCount: 6);
        fixture.Redis.Setup(service => service.GetBusinessConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessConfig?)null);
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SemanticSearchFixture.Rerank(
                fixture.Products.Select((product, index) => (product.Id.ToString(), 0.99f - index * 0.01f)).ToArray()));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.Data.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_WhenRerankerFails_ReturnsOriginalFailure()
    {
        var fixture = new SemanticSearchFixture();
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<ICollection<RankedRecord>>>.Failure(502, "Reranker failed"));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.StatusCode.Should().Be(502);
        result.Message.Should().Be("Reranker failed");
    }

    [Fact]
    public async Task Handle_AppliesConfiguredScoreAndTopKBeforeMapping()
    {
        var fixture = new SemanticSearchFixture(productCount: 4);
        fixture.Redis.Setup(service => service.GetBusinessConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessConfig { TopKDocument = 2, RerankingScore = 0.8 });
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SemanticSearchFixture.Rerank(
                (fixture.Products[0].Id.ToString(), 0.70f),
                (fixture.Products[1].Id.ToString(), 0.99f),
                (fixture.Products[2].Id.ToString(), 0.85f),
                (fixture.Products[3].Id.ToString(), 0.95f)));

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.Data!.Select(product => product.ProductId)
            .Should().Equal(fixture.Products[1].Id.ToString(), fixture.Products[3].Id.ToString());
    }

    [Fact]
    public async Task Handle_ValidRequest_MapsRankedProductsAndReturnsSuccessMessage()
    {
        var fixture = new SemanticSearchFixture();

        var result = await fixture.Handler.Handle(fixture.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Product semantic search successfully.");
        result.Data.Should().NotBeEmpty();
    }

    private sealed class SemanticSearchFixture
    {
        public Business Business { get; } = TestData.Business();
        public List<Product> Products { get; } = [];
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IGeminiService> Gemini { get; } = new();
        public Mock<IQdrantService> Qdrant { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IRedisBusinessConfig> Redis { get; } = new();
        public ProductSemanticSearchQueryHandler Handler { get; }

        public SemanticSearchFixture(bool emptyPoints = false, int productCount = 2)
        {
            for (var index = 0; index < productCount; index++)
            {
                var product = TestData.Product(Business);
                product.Id = ObjectId.GenerateNewId();
                product.ExternalId = $"SKU-{index + 1:000}";
                product.Name = $"Product {index + 1}";
                Products.Add(product);
            }

            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            Gemini.Setup(service => service.EmbeddingsAsyncV3(
                    It.IsAny<IReadOnlyList<string>>(), "RETRIEVAL_QUERY", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<GeminiResponse<IReadOnlyList<double[]>>>.Success(new GeminiResponse<IReadOnlyList<double[]>>
                {
                    Result = new List<double[]> { new[] { 1d, 2d }, new[] { 3d, 4d } },
                    InputTokens = 4
                }));
            var points = emptyPoints
                ? new List<ScoredPoint>()
                : Products.Select(product => Point(product.Id.ToString())).ToList();
            Qdrant.Setup(service => service.HybridSearchAsync(
                    It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<Filter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(points);
            ProductRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(Products);
            Redis.Setup(service => service.GetBusinessConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BusinessConfig { TopKDocument = 5, RerankingScore = 0.75 });
            Gemini.Setup(service => service.RerankerAsyncV2(
                    It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Rerank(Products.Select(product => (product.Id.ToString(), 0.95f)).ToArray()));

            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<List<ProductResponseV2>>(It.IsAny<object>()))
                .Returns((object source) => ((IEnumerable<Product>)source).Select(product => new ProductResponseV2
                {
                    ProductId = product.Id.ToString(),
                    Name = product.Name,
                    ExternalProductUrl = product.ExternalProductUrl,
                    Category = product.Category
                }).ToList());

            Handler = new ProductSemanticSearchQueryHandler(
                CurrentUser.Object, Gemini.Object, Qdrant.Object, mapper.Object,
                Mock.Of<ILogger<ProductSemanticSearchQueryHandler>>(), Redis.Object, ProductRepository.Object);
        }

        public ProductSemanticSearchQuery Query(
            decimal? minimum = null,
            decimal? maximum = null,
            IEnumerable<string>? excludeProductIds = null) => new()
        {
            Request = new ProductSemanticSearchRequest
            {
                SemanticQuery = "gaming laptop",
                TechnicalQuery = "laptop RTX",
                MinPrice = minimum,
                MaxPrice = maximum,
                ExcludeProductIds = excludeProductIds?.ToList() ?? []
            }
        };

        public static ScoredPoint Point(string? productId)
        {
            var point = new ScoredPoint();
            if (productId is not null)
            {
                point.Payload[ProductPayloadNames.ProductId] = new Value { StringValue = productId };
            }
            return point;
        }

        public static Result<GeminiResponse<ICollection<RankedRecord>>> Rerank(
            params (string Id, float Score)[] records) =>
            Result<GeminiResponse<ICollection<RankedRecord>>>.Success(
                new GeminiResponse<ICollection<RankedRecord>>
                {
                    Result = records.Select(record => new RankedRecord { Id = record.Id, Score = record.Score }).ToList()
                });
    }
}

public class UT_ProductEmbed
{
    [Fact]
    public async Task Handle_WhenProductIdInvalid_ReturnsBadRequestBeforeLookup()
    {
        var fixture = new ProductEmbedFixture();

        var result = await fixture.Handler.Handle(new ProductEmbedCommand { ProductId = "bad-id" }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.ProductRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Product, bool>>>(),
            It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductMissing_ReturnsNotFound()
    {
        var fixture = new ProductEmbedFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync((Product?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenProductAlreadyEmbedded_ReturnsCurrentProductWithoutExternalCalls()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Product.Status = ProductStatus.Active;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Gemini.VerifyNoOtherCalls();
        fixture.Qdrant.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenSearchContentMissing_ReturnsNotFound()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Product.SearchContent = null;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_WhenSemanticTextGenerationFails_ReturnsServerFailure()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Gemini.Setup(service => service.GenerateTextAsyncV2(
                It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<string>>.Failure(503, "generation failed"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.Gemini.Verify(service => service.EmbeddingsAsyncV2(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTechnicalEmbeddingFails_ReturnsServerFailure()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Gemini.Setup(service => service.EmbeddingsAsyncV2(
                It.IsAny<string>(), "RETRIEVAL_DOCUMENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<double[]>>.Failure(503, "vector failed"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.Qdrant.Verify(service => service.UpsertAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSemanticEmbeddingFails_ReturnsServerFailure()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Gemini.SetupSequence(service => service.EmbeddingsAsyncV2(
                It.IsAny<string>(), "RETRIEVAL_DOCUMENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductEmbedFixture.Vector(9))
            .ReturnsAsync(Result<GeminiResponse<double[]>>.Failure(503, "semantic vector failed"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Handle_WhenBusinessQuotaMissing_ReturnsNotFoundBeforeQdrantWrite()
    {
        var fixture = new ProductEmbedFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Qdrant.Verify(service => service.UpsertAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidProduct_ActivatesProductChargesTokensAndPersistsQdrantPoint()
    {
        var fixture = new ProductEmbedFixture();
        UsageQuotaLog? savedLog = null;
        IReadOnlyList<PointStruct>? savedPoints = null;
        fixture.UsageRepository.Setup(repository => repository.AddAsync(It.IsAny<UsageQuotaLog>()))
            .Callback<UsageQuotaLog>(value => savedLog = value)
            .Returns(Task.CompletedTask);
        fixture.Qdrant.Setup(service => service.UpsertAsync(
                QdrantCollections.Products, It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<PointStruct>, CancellationToken>((_, points, _) => savedPoints = points)
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Product.Status.Should().Be(ProductStatus.Active);
        fixture.Product.EmbbbedAt.Should().Be(TestData.Now);
        fixture.Quota.UsedTokens.Should().Be(8);
        savedLog!.InputTokens.Should().Be(8);
        savedLog.BillableTokens.Should().Be(16);
        savedLog.SourceType.Should().Be(SourceTypeEnum.EmbeddingProduct);
        savedPoints.Should().ContainSingle();
        savedPoints![0].Vectors.Vectors_.Vectors.Keys.Should()
            .Contain([ProductVectorNames.ProductTechnical, ProductVectorNames.SemanticSearch]);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenQdrantUpsertThrows_ReturnsServerFailureWithoutRepositoryUpdates()
    {
        var fixture = new ProductEmbedFixture();
        fixture.Qdrant.Setup(service => service.UpsertAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<PointStruct>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("qdrant unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.ProductRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryUpdateThrows_ReturnsServerFailureAndSkipsSaveChanges()
    {
        var fixture = new ProductEmbedFixture();
        fixture.ProductRepository.Setup(repository => repository.UpdateAsync(It.IsAny<Product>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ProductEmbedFixture
    {
        public Business Business { get; } = TestData.Business();
        public Product Product { get; }
        public BusinessQuota Quota { get; }
        public Mock<IQdrantService> Qdrant { get; } = new();
        public Mock<IGeminiService> Gemini { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IUsageQuotaLogRepository> UsageRepository { get; } = new();
        public ProductEmbedCommandHandler Handler { get; }

        public ProductEmbedFixture()
        {
            Product = TestData.Product(Business, ProductStatus.PendingEmbedding);
            Product.SearchContent = "Laptop gaming RTX";
            Quota = TestData.Quota(Business);
            Quota.UsedTokens = 0;

            ProductRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(Product);
            Gemini.Setup(service => service.GenerateTextAsyncV2(
                    It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<GeminiResponse<string>>.Success(new GeminiResponse<string>
                {
                    Result = "Semantic laptop description",
                    InputTokens = 6,
                    OutputTokens = 4
                }));
            Gemini.Setup(service => service.EmbeddingsAsyncV2(
                    It.IsAny<string>(), "RETRIEVAL_DOCUMENT", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Vector(9));
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id)).ReturnsAsync(Quota);

            Handler = new ProductEmbedCommandHandler(
                Mock.Of<ILogger<ProductEmbedCommandHandler>>(), Qdrant.Object, Gemini.Object,
                ProductRepository.Object, Mock.Of<IQwenService>(), UnitOfWork.Object,
                QuotaRepository.Object, UsageRepository.Object, new FixedTimeProvider(TestData.Now));
        }

        public ProductEmbedCommand Command() => new()
        {
            ProductId = Product.Id.ToString(),
            QdrantPointId = Product.QdrantPointId
        };

        public static Result<GeminiResponse<double[]>> Vector(long inputTokens) =>
            Result<GeminiResponse<double[]>>.Success(new GeminiResponse<double[]>
            {
                Result = new[] { 0.1, 0.2, 0.3 },
                InputTokens = inputTokens
            });
    }
}
