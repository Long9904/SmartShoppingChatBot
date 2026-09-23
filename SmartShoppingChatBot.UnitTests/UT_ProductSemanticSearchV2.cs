using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;
using SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductSemanticSearchV2;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.UnitTests;

public sealed class UT_ProductSemanticSearchV2
{
    [Fact]
    public async Task Handle_AppliesSchemaPriceAndExcludeBeforeThreeWaySearch_ThenKeepsLowRerankScore()
    {
        var business = TestData.Business();
        var product = TestData.Product(business);
        product.Id = ObjectId.GenerateNewId();
        product.Price = 350000m;
        product.Metadata["color"] = "white";
        var excludedId = ObjectId.GenerateNewId();
        var schema = new CategoryAttributeSchema
        {
            Category = "thời trang > áo",
            IsActive = true,
            Attributes =
            [
                new AttributeDefinition
                {
                    Key = "color",
                    DisplayName = "Màu sắc",
                    DataType = AttributeDataType.Keyword,
                    IsFilterable = true,
                    AllowedValues = ["white", "black"]
                }
            ]
        };
        var currentUser = new Mock<ICurrentUserService>();
        var schemas = new Mock<ICategoryAttributeSchemaService>();
        var redis = new Mock<IRedisBusinessConfig>();
        var gemini = new Mock<IGeminiService>();
        var qdrant = new Mock<IQdrantService>();
        var products = new Mock<IProductRepository>();
        Filter? capturedFilter = null;

        currentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Success(business));
        schemas.Setup(service => service.GetActiveSchemaAsync(schema.Category))
            .ReturnsAsync(schema);
        redis.Setup(service => service.GetBusinessConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessConfig
            {
                TopKDocument = 5,
                RerankingScore = 0.99,
                MediumPriceMinLimit = 200000m,
                MediumPriceMaxLimit = 500000m
            });
        gemini.Setup(service => service.EmbeddingsAsyncV3(
                It.Is<IReadOnlyList<string>>(queries => queries.SequenceEqual(new[] { "áo đi làm", "áo sơ mi white" })),
                "RETRIEVAL_QUERY",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(VectorResult());
        qdrant.Setup(service => service.HybridProductSearchV2Async(
                It.IsAny<float[]>(),
                It.IsAny<float[]>(),
                "áo sơ mi",
                It.IsAny<Filter>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<float[], float[], string, Filter, int, CancellationToken>(
                (_, _, _, filter, _, _) => capturedFilter = filter)
            .ReturnsAsync([Point(product.Id)]);
        products.Setup(repository => repository.FindAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync([product]);
        gemini.Setup(service => service.RerankerAsyncV2(
                "áo đi làm",
                It.IsAny<IEnumerable<RankRecord>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RerankResult((product.Id.ToString(), 0.05f)));

        var handler = new ProductSemanticSearchV2QueryHandler(
            currentUser.Object,
            schemas.Object,
            redis.Object,
            gemini.Object,
            qdrant.Object,
            products.Object,
            Mock.Of<ILogger<ProductSemanticSearchV2QueryHandler>>());
        var result = await handler.Handle(new ProductSemanticSearchV2Query
        {
            Request = new ProductSemanticSearchV2Request
            {
                SemanticQuery = "áo đi làm",
                TechnicalQuery = "áo sơ mi white",
                Bm25Query = "áo sơ mi",
                Category = schema.Category,
                Attributes = [new CategoryAttributeFilterRequest { Name = "color", Value = "white" }],
                PriceBand = CategoryPriceBand.Medium,
                ExcludeProductIds = [excludedId.ToString()]
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].ProductId.Should().Be(product.Id.ToString());
        result.Data[0].Score.Should().BeApproximately(0.05, 0.000001);
        capturedFilter.Should().NotBeNull();
        capturedFilter!.Must.Should().Contain(condition =>
            condition.Field.Key == ProductPayloadNames.Category
            && condition.Field.Match.Keyword == schema.Category);
        capturedFilter.Must.Should().Contain(condition =>
            condition.Field.Key == "color"
            && condition.Field.Match.Keyword == "white");
        capturedFilter.Must.Should().Contain(condition =>
            condition.Field.Key == ProductPayloadNames.Price
            && condition.Field.Range.Gte == 200000d
            && condition.Field.Range.Lte == 500000d);
        capturedFilter.MustNot.Should().Contain(condition =>
            condition.Field.Key == ProductPayloadNames.ProductId
            && condition.Field.Match.Keyword == excludedId.ToString());
    }

    [Fact]
    public async Task GetByExternalIds_PreservesInputOrderAndScopesToCurrentBusiness()
    {
        var business = TestData.Business();
        var first = TestData.Product(business);
        first.ExternalId = "SKU-001";
        var second = TestData.Product(business);
        second.Id = ObjectId.GenerateNewId();
        second.ExternalId = "SKU-002";
        var otherBusiness = TestData.Business();
        otherBusiness.Id = ObjectId.GenerateNewId();
        var foreign = TestData.Product(otherBusiness);
        foreign.ExternalId = "SKU-002";
        var currentUser = new Mock<ICurrentUserService>();
        var repository = new Mock<IProductRepository>();
        currentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Success(business));
        repository.Setup(service => service.FindAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate,
                    Func<IQueryable<Product>, IQueryable<Product>>? _) =>
                new[] { first, second, foreign }.Where(predicate.Compile()).ToList());
        var handler = new ProductGetByExternalIdsQueryHandler(currentUser.Object, repository.Object);

        var result = await handler.Handle(new ProductGetByExternalIdsQuery
        {
            Request = new ProductGetByExternalIdsRequest
            {
                ExternalProductIds = ["SKU-002", "SKU-001", "SKU-002"]
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Select(product => product.ExternalProductId)
            .Should().Equal("SKU-002", "SKU-001");
        result.Data!.Select(product => product.ProductId)
            .Should().Equal(second.Id.ToString(), first.Id.ToString());
    }

    private static Result<GeminiResponse<IReadOnlyList<double[]>>> VectorResult() =>
        Result<GeminiResponse<IReadOnlyList<double[]>>>.Success(
            new GeminiResponse<IReadOnlyList<double[]>>
            {
                Result = new List<double[]> { new[] { 1d, 2d }, new[] { 3d, 4d } }
            });

    private static Result<GeminiResponse<ICollection<RankedRecord>>> RerankResult(
        params (string Id, float Score)[] records) =>
        Result<GeminiResponse<ICollection<RankedRecord>>>.Success(
            new GeminiResponse<ICollection<RankedRecord>>
            {
                Result = records.Select(record => new RankedRecord
                {
                    Id = record.Id,
                    Score = record.Score
                }).ToList()
            });

    private static ScoredPoint Point(ObjectId productId)
    {
        var point = new ScoredPoint();
        point.Payload[ProductPayloadNames.ProductId] = new Value
        {
            StringValue = productId.ToString()
        };
        return point;
    }
}
