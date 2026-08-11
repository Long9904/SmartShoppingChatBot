using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetById;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ProductReadQueries
{
    [Fact]
    public async Task GetAll_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ProductReadFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.GetAllHandler.Handle(new ProductGetAllQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.ProductRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetAll_AlwaysScopesToBusinessAndExcludesDeletedProducts()
    {
        var fixture = new ProductReadFixture();
        var otherBusinessProduct = TestData.Product(TestData.Business());
        var deleted = TestData.Product(fixture.Business, ProductStatus.Deleted);
        fixture.Products.AddRange([otherBusinessProduct, deleted]);

        var result = await fixture.GetAllHandler.Handle(new ProductGetAllQuery(), CancellationToken.None);

        result.Data!.Items.Should().OnlyContain(product =>
            product.Id == fixture.Products[0].Id.ToString() || product.Id == fixture.Products[1].Id.ToString());
    }

    [Fact]
    public async Task GetAll_AppliesCombinedSearchPriceStockStatusAndPaginationFilters()
    {
        var fixture = new ProductReadFixture();
        var query = new ProductGetAllQuery
        {
            Filter = new ProductGetAllFilter
            {
                ExternalId = fixture.Products[0].ExternalId,
                Name = "Product",
                MinPrice = 100,
                MaxPrice = 1000,
                MinStockQuantity = 1,
                MaxStockQuantity = 10,
                Category = "Laptop",
                Status = ProductStatus.Active,
                PageIndex = 2,
                PageSize = 5
            }
        };

        var result = await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().ContainSingle();
        result.Data.PageIndex.Should().Be(2);
        result.Data.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetAll_WhenDeletedStatusRequested_ReturnsEmptyPage()
    {
        var fixture = new ProductReadFixture();
        var query = new ProductGetAllQuery { Filter = new ProductGetAllFilter { Status = ProductStatus.Deleted } };

        var result = await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("name asc", "Product A")]
    [InlineData("name desc", "Product B")]
    [InlineData("price asc", "Product A")]
    [InlineData("price desc", "Product B")]
    public async Task GetAll_AppliesRequestedOrdering(string orderBy, string expectedFirstName)
    {
        var fixture = new ProductReadFixture();
        var query = new ProductGetAllQuery { Filter = new ProductGetAllFilter { OrderBy = orderBy } };

        var result = await fixture.GetAllHandler.Handle(query, CancellationToken.None);

        result.Data!.Items.First().Name.Should().Be(expectedFirstName);
    }

    [Fact]
    public async Task GetById_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ProductReadFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.GetByIdHandler.Handle(
            new ProductGetByIdQuery { ProductId = fixture.Products[0].Id }, CancellationToken.None);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetById_WhenProductMissing_ReturnsNotFound()
    {
        var fixture = new ProductReadFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync((Product?)null);

        var result = await fixture.GetByIdHandler.Handle(
            new ProductGetByIdQuery { ProductId = ObjectId.GenerateNewId() }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetById_WithProductId_UsesBusinessAndDeletedGuards()
    {
        var fixture = new ProductReadFixture();
        Expression<Func<Product, bool>>? captured = null;
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .Callback<Expression<Func<Product, bool>>, Func<IQueryable<Product>, IQueryable<Product>>?>((predicate, _) => captured = predicate)
            .ReturnsAsync(fixture.Products[0]);

        var result = await fixture.GetByIdHandler.Handle(
            new ProductGetByIdQuery { ProductId = fixture.Products[0].Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Compile()(fixture.Products[0]).Should().BeTrue();
        captured.Compile()(TestData.Product(TestData.Business())).Should().BeFalse();
    }

    [Fact]
    public async Task GetById_WithExternalId_ReturnsMappedProduct()
    {
        var fixture = new ProductReadFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync(fixture.Products[1]);

        var result = await fixture.GetByIdHandler.Handle(
            new ProductGetByIdQuery { ExternalId = fixture.Products[1].ExternalId }, CancellationToken.None);

        result.Data!.Id.Should().Be(fixture.Products[1].Id.ToString());
        result.Data.Name.Should().Be("Product B");
    }

    [Fact]
    public async Task GetByIds_WhenAnyIdInvalid_ReturnsBadRequestWithoutRepositoryCall()
    {
        var fixture = new ProductReadFixture();
        var query = new ProductGetByIdsQuery { ProductIds = [fixture.Products[0].Id.ToString(), "invalid"] };

        var result = await fixture.GetByIdsHandler.Handle(query, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.ProductRepository.Verify(repository => repository.FindAllAsync(
            It.IsAny<Expression<Func<Product, bool>>>(),
            It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()), Times.Never);
    }

    [Fact]
    public async Task GetByIds_DeduplicatesAndPreservesRequestedOrder()
    {
        var fixture = new ProductReadFixture();
        var query = new ProductGetByIdsQuery
        {
            ProductIds =
            [
                $" {fixture.Products[1].Id} ",
                fixture.Products[0].Id.ToString(),
                fixture.Products[1].Id.ToString()
            ]
        };

        var result = await fixture.GetByIdsHandler.Handle(query, CancellationToken.None);

        result.Data!.Select(product => product.ProductId)
            .Should().Equal(fixture.Products[1].Id.ToString(), fixture.Products[0].Id.ToString());
    }

    [Fact]
    public async Task GetByIds_WhenNoProductsMatch_ReturnsEmptySuccessMessage()
    {
        var fixture = new ProductReadFixture();
        fixture.ProductRepository.Setup(repository => repository.FindAllAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
            .ReturnsAsync([]);

        var result = await fixture.GetByIdsHandler.Handle(
            new ProductGetByIdsQuery { ProductIds = [ObjectId.GenerateNewId().ToString()] }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("No matching products found.");
    }

    private sealed class ProductReadFixture
    {
        public Business Business { get; } = TestData.Business();
        public List<Product> Products { get; } = [];
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public ProductGetAllQueryHandler GetAllHandler { get; }
        public ProductGetByIdQueryHandler GetByIdHandler { get; }
        public ProductGetByIdsQueryHandler GetByIdsHandler { get; }

        public ProductReadFixture()
        {
            var first = TestData.Product(Business);
            first.Name = "Product A";
            first.ExternalId = "A-001";
            first.Price = 100;
            first.StockQuantity = 2;
            first.Category = "Laptop";
            first.UpdatedAt = TestData.Now.AddMinutes(-2);
            var second = TestData.Product(Business);
            second.Name = "Product B";
            second.ExternalId = "B-001";
            second.Price = 500;
            second.StockQuantity = 5;
            second.Category = "Laptop";
            second.UpdatedAt = TestData.Now.AddMinutes(-1);
            Products.AddRange([first, second]);

            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            ProductRepository.Setup(repository => repository.AsQueryable()).Returns(() => Products.AsQueryable());
            ProductRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<Product>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<Product> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<Product>(items, items.Count, index, size);
                });
            ProductRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(first);
            ProductRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync(Products);

            var mapper = new Mock<IMapper>();
            mapper.Setup(value => value.Map<List<ProductResponseV2>>(It.IsAny<object>()))
                .Returns((object source) => ((IEnumerable<Product>)source).Select(product => new ProductResponseV2
                {
                    ProductId = product.Id.ToString(), Name = product.Name,
                    ExternalProductUrl = product.ExternalProductUrl, Category = product.Category
                }).ToList());
            GetAllHandler = new ProductGetAllQueryHandler(CurrentUser.Object, ProductRepository.Object);
            GetByIdHandler = new ProductGetByIdQueryHandler(CurrentUser.Object, ProductRepository.Object);
            GetByIdsHandler = new ProductGetByIdsQueryHandler(CurrentUser.Object, ProductRepository.Object, mapper.Object);
        }
    }
}

public class UT_ImportJobQuery
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsOriginalFailure()
    {
        var fixture = new ImportJobQueryFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(new GetImportJobsQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Repository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task Handle_AlwaysScopesJobsToCurrentBusiness()
    {
        var fixture = new ImportJobQueryFixture();
        fixture.Jobs.Add(new ImportJob { Id = ObjectId.GenerateNewId(), BusinessId = TestData.Business().Id, FileName = "other.xlsx" });

        var result = await fixture.Handler.Handle(new GetImportJobsQuery(), CancellationToken.None);

        result.Data!.Items.Should().OnlyContain(job => job.FileName != "other.xlsx");
    }

    [Fact]
    public async Task Handle_TrimsFileNameAndAppliesStatusFilter()
    {
        var fixture = new ImportJobQueryFixture();
        var query = new GetImportJobsQuery
        {
            Filter = new GetImportJobsFilter { FileName = " import ", Status = ImportJobStatus.Completed }
        };

        var result = await fixture.Handler.Handle(query, CancellationToken.None);

        result.Data!.Items.Should().ContainSingle();
        result.Data.Items.Single().Status.Should().Be(ImportJobStatus.Completed);
    }

    [Fact]
    public async Task Handle_OrdersNewestFirstAndPassesPagination()
    {
        var fixture = new ImportJobQueryFixture();
        var query = new GetImportJobsQuery { Filter = new GetImportJobsFilter { PageIndex = 3, PageSize = 4 } };

        var result = await fixture.Handler.Handle(query, CancellationToken.None);

        result.Data!.Items.First().FileName.Should().Be("import-new.xlsx");
        result.Data.PageIndex.Should().Be(3);
        result.Data.PageSize.Should().Be(4);
    }

    [Fact]
    public async Task Handle_MapsProgressErrorsAndTimestamps()
    {
        var fixture = new ImportJobQueryFixture();

        var result = await fixture.Handler.Handle(new GetImportJobsQuery(), CancellationToken.None);

        var mapped = result.Data!.Items.First();
        mapped.TotalRows.Should().Be(10);
        mapped.ProcessedRows.Should().Be(8);
        mapped.SuccessRows.Should().Be(7);
        mapped.FailedRows.Should().Be(1);
        mapped.EmbeddedRows.Should().Be(6);
        mapped.CreatedAt.Should().Be(TestData.Now);
    }

    private sealed class ImportJobQueryFixture
    {
        public Business Business { get; } = TestData.Business();
        public List<ImportJob> Jobs { get; } = [];
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IImportJobRepository> Repository { get; } = new();
        public GetImportJobsQueryHandler Handler { get; }

        public ImportJobQueryFixture()
        {
            Jobs.Add(new ImportJob
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, FileName = "import-new.xlsx",
                Status = ImportJobStatus.Completed, TotalRows = 10, ProcessedRows = 8,
                SuccessRows = 7, FailedRows = 1, EmbeddedRows = 6, CreatedAt = TestData.Now
            });
            Jobs.Add(new ImportJob
            {
                Id = ObjectId.GenerateNewId(), BusinessId = Business.Id, FileName = "old.xlsx",
                Status = ImportJobStatus.Pending, CreatedAt = TestData.Now.AddDays(-1)
            });
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            Repository.Setup(repository => repository.AsQueryable()).Returns(() => Jobs.AsQueryable());
            Repository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<ImportJob>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<ImportJob> query, int index, int size) =>
                {
                    var items = query.ToList();
                    return new BasePaginatedList<ImportJob>(items, items.Count, index, size);
                });
            Handler = new GetImportJobsQueryHandler(CurrentUser.Object, Repository.Object);
        }
    }
}
