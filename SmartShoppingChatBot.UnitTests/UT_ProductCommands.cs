using System.Linq.Expressions;
using System.Security.Claims;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductDelete;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductUpdate;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ProductCreate
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsFailureWithoutRepositoryCalls()
    {
        var fixture = new ProductCreateFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid token"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        fixture.ProductRepository.Verify(repository => repository.AddAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExternalIdExists_ReturnsConflict()
    {
        var fixture = new ProductCreateFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync(TestData.Product(fixture.Business));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(409);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenQuotaMissing_ReturnsNotFound()
    {
        var fixture = new ProductCreateFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.ProductRepository.Verify(repository => repository.AddAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductCountEqualsMaximum_ReturnsRateLimit()
    {
        var fixture = new ProductCreateFixture();
        fixture.ProductRepository.Setup(repository => repository.CountAsync(It.IsAny<Expression<Func<Product, bool>>>() ))
            .ReturnsAsync(fixture.Quota.MaxProductAllowed);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenRemainingTokensAreBelowEmbeddingBudget_ReturnsTooManyRequests()
    {
        var fixture = new ProductCreateFixture();
        fixture.Quota.TokenLimit = 10_000;
        fixture.Quota.UsedTokens = 6_501;

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(429);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<ProductCreateEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ApiKeyRequest_CreatesPendingProductWithBusinessActorAndPublishesEvent()
    {
        var fixture = new ProductCreateFixture(authenticationType: "ApiKey");
        Product? savedProduct = null;
        fixture.ProductRepository.Setup(repository => repository.AddAsync(It.IsAny<Product>()))
            .Callback<Product>(product => savedProduct = product)
            .Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        savedProduct!.Status.Should().Be(ProductStatus.PendingEmbedding);
        savedProduct.CreatedBy.Name.Should().Be("Business: " + fixture.Business.BusinessName);
        savedProduct.SearchContent.Should().Contain("Laptop").And.Contain("ram: 16GB");
        fixture.UnitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.Is<ProductCreateEvent>(message => message.ProductId == savedProduct.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InternalRequest_WhenUserFails_ReturnsFailureBeforeTransaction()
    {
        var fixture = new ProductCreateFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveThrows_RollsBackAndReturnsServerError()
    {
        var fixture = new ProductCreateFixture(authenticationType: "ApiKey");
        fixture.UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        fixture.UnitOfWork.Verify(unit => unit.RollBackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<ProductCreateEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class ProductCreateFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public BusinessQuota Quota { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public ProductCreateCommandHandler Handler { get; }

        public ProductCreateFixture(string authenticationType = "Bearer")
        {
            User = TestData.User(Business);
            Quota = TestData.Quota(Business);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            ProductRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync((Product?)null);
            ProductRepository.Setup(repository => repository.CountAsync(It.IsAny<Expression<Func<Product, bool>>>() ))
                .ReturnsAsync(0);
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id)).ReturnsAsync(Quota);
            var context = TestData.HttpContext(authenticationType,
                new Claim(ClaimTypes.NameIdentifier, User.Id.ToString()));
            Handler = new ProductCreateCommandHandler(
                Mock.Of<ILogger<ProductCreateCommandHandler>>(), CurrentUser.Object,
                ProductRepository.Object, UnitOfWork.Object, Publisher.Object,
                new FixedTimeProvider(TestData.Now), context, QuotaRepository.Object);
        }

        public ProductCreateCommand Command() => new()
        {
            ExternalId = "SKU-001",
            Name = "Laptop",
            Description = "Gaming laptop",
            ExternalProductUrl = "https://shop.example/p/1",
            Price = 25_000_000,
            Currency = "VND",
            Brand = "Brand A",
            StockQuantity = 5,
            Category = "Laptop",
            Images = ["https://shop.example/1.png"],
            Metadata = new Dictionary<string, string> { ["ram"] = "16GB" }
        };
    }
}

public class UT_ProductUpdate
{
    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ReturnsNotFound()
    {
        var fixture = new ProductUpdateFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync((Product?)null);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNewExternalIdConflicts_ReturnsConflict()
    {
        var fixture = new ProductUpdateFixture();
        var conflict = TestData.Product(fixture.Business);
        var calls = 0;
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync(() => calls++ == 0 ? fixture.Product : conflict);
        var command = fixture.Command();
        command.ExternalId = "SKU-NEW";

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(409);
        fixture.ProductRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PriceOnlyChange_UpdatesQdrantPayloadWithoutPublishingReembeddingEvent()
    {
        var fixture = new ProductUpdateFixture();
        var command = fixture.Command();
        command.Price += 1_000;

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Product.Price.Should().Be(command.Price);
        fixture.Qdrant.Verify(service => service.SetPayloadAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Dictionary<string, Qdrant.Client.Grpc.Value>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<ProductCreateEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NameChange_WhenQuotaMissing_ReturnsNotFound()
    {
        var fixture = new ProductUpdateFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);
        var command = fixture.Command();
        command.Name = "Updated laptop";

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.ProductRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NameChange_WhenQuotaInsufficient_ReturnsTooManyRequests()
    {
        var fixture = new ProductUpdateFixture();
        fixture.Quota.TokenLimit = 3_499;
        var command = fixture.Command();
        command.Name = "Updated laptop";

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Handle_NameChange_QueuesReembeddingAndPublishesEvent()
    {
        var fixture = new ProductUpdateFixture();
        var command = fixture.Command();
        command.Name = "Updated laptop";

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Product.Status.Should().Be(ProductStatus.PendingEmbedding);
        fixture.Product.SearchContent.Should().Contain("Updated laptop");
        fixture.Qdrant.Verify(service => service.SetPayloadAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Dictionary<string, Qdrant.Client.Grpc.Value>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.Is<ProductCreateEvent>(message => message.ProductId == fixture.Product.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class ProductUpdateFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Product Product { get; }
        public BusinessQuota Quota { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IQdrantService> Qdrant { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public ProductUpdateCommandHandler Handler { get; }

        public ProductUpdateFixture()
        {
            User = TestData.User(Business);
            Product = TestData.Product(Business);
            Quota = TestData.Quota(Business);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            ProductRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(Product);
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id)).ReturnsAsync(Quota);
            var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, User.Id.ToString()));
            Handler = new ProductUpdateCommandHandler(
                CurrentUser.Object, context, ProductRepository.Object, UnitOfWork.Object,
                Qdrant.Object, QuotaRepository.Object, Publisher.Object,
                new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<ProductUpdateCommandHandler>>());
        }

        public ProductUpdateCommand Command() => new()
        {
            ProductId = Product.Id,
            ExternalId = Product.ExternalId,
            Name = Product.Name,
            Description = Product.Description,
            ExternalProductUrl = Product.ExternalProductUrl,
            Price = Product.Price,
            Currency = Product.Currency,
            Brand = Product.Brand,
            StockQuantity = Product.StockQuantity,
            Category = Product.Category,
            Images = [.. Product.Images],
            Metadata = new Dictionary<string, string>(Product.Metadata)
        };
    }
}

public class UT_ProductDelete
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsFailureBeforeLookup()
    {
        var fixture = new ProductDeleteFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = fixture.Product.Id }, CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.ProductRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Product, bool>>>(),
            It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductMissing_ReturnsNotFound()
    {
        var fixture = new ProductDeleteFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync((Product?)null);

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = ObjectId.GenerateNewId() }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Handle_PendingProduct_SoftDeletesWithoutUpdatingQdrant()
    {
        var fixture = new ProductDeleteFixture(ProductStatus.PendingEmbedding);

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = fixture.Product.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Product.Status.Should().Be(ProductStatus.Deleted);
        fixture.Qdrant.Verify(service => service.SetPayloadAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<Dictionary<string, Qdrant.Client.Grpc.Value>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmbeddedProduct_SoftDeletesAndMarksQdrantPayloadDeleted()
    {
        var fixture = new ProductDeleteFixture(ProductStatus.Active);

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = fixture.Product.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Product.Status.Should().Be(ProductStatus.Deleted);
        fixture.Qdrant.Verify(service => service.SetPayloadAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.Is<Dictionary<string, Qdrant.Client.Grpc.Value>>(payload => payload.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryUpdateThrows_ReturnsServerFailure()
    {
        var fixture = new ProductDeleteFixture();
        fixture.ProductRepository.Setup(repository => repository.UpdateAsync(It.IsAny<Product>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = fixture.Product.Id }, CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenActorResolutionFails_ReturnsFailureWithoutDeleting()
    {
        var fixture = new ProductDeleteFixture();
        fixture.CurrentUser.Setup(service => service.GetUser())
            .ReturnsAsync(Result<User>.Failure(401, "Invalid user"));

        var result = await fixture.Handler.Handle(new ProductDeleteCommand { ProductId = fixture.Product.Id }, CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Product.Status.Should().Be(ProductStatus.Active);
        fixture.ProductRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    private sealed class ProductDeleteFixture
    {
        public Business Business { get; } = TestData.Business();
        public User User { get; }
        public Product Product { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IQdrantService> Qdrant { get; } = new();
        public ProductDeleteCommandHandler Handler { get; }

        public ProductDeleteFixture(ProductStatus status = ProductStatus.Active)
        {
            User = TestData.User(Business);
            Product = TestData.Product(Business, status);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            CurrentUser.Setup(service => service.GetUser()).ReturnsAsync(Result<User>.Success(User));
            ProductRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(Product);
            var context = TestData.HttpContext("Bearer", new Claim(ClaimTypes.NameIdentifier, User.Id.ToString()));
            Handler = new ProductDeleteCommandHandler(
                CurrentUser.Object, context, ProductRepository.Object, UnitOfWork.Object,
                Qdrant.Object, new FixedTimeProvider(TestData.Now), Mock.Of<ILogger<ProductDeleteCommandHandler>>());
        }
    }
}
