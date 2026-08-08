using System.Linq.Expressions;
using ClosedXML.Excel;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ProductManagement.ImportProductExcel;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.UnitTests;

public class UT_ProductImport
{
    [Fact]
    public async Task Handle_WhenBusinessFails_ReturnsFailureBeforeCreatingImportJob()
    {
        var fixture = new ImportFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.JobRepository.Verify(repository => repository.AddAsync(It.IsAny<ImportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuotaMissing_ReturnsNotFoundBeforeCreatingImportJob()
    {
        var fixture = new ImportFixture();
        fixture.QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.JobRepository.Verify(repository => repository.AddAsync(It.IsAny<ImportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInitialTokenBudgetBelowOneProduct_ReturnsTooManyRequests()
    {
        var fixture = new ImportFixture();
        fixture.Quota.TokenLimit = 3_499;

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(429);
        fixture.JobRepository.Verify(repository => repository.AddAsync(It.IsAny<ImportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRequiredHeaderMissing_MarksJobFailed()
    {
        var fixture = new ImportFixture();
        ImportJob? job = null;
        fixture.JobRepository.Setup(repository => repository.AddAsync(It.IsAny<ImportJob>()))
            .Callback<ImportJob>(value => job = value).Returns(Task.CompletedTask);
        var file = ExcelFile(headers => headers.Remove("Variant Price"));

        var result = await fixture.Handler.Handle(new ImportProductsCommand(file), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        job!.Status.Should().Be(ImportJobStatus.Failed);
        job.Errors.Should().ContainSingle(error => error.Field == "File" && error.Message.Contains("Variant Price"));
    }

    [Fact]
    public async Task Handle_WhenHeaderDuplicated_MarksJobFailed()
    {
        var fixture = new ImportFixture();
        ImportJob? job = null;
        fixture.JobRepository.Setup(repository => repository.AddAsync(It.IsAny<ImportJob>()))
            .Callback<ImportJob>(value => job = value).Returns(Task.CompletedTask);
        var file = ExcelFile(extraHeaders: ["Title"]);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(file), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        job!.Errors.Should().ContainSingle(error => error.Message.Contains("bị trùng"));
    }

    [Fact]
    public async Task Handle_WhenWorkbookHasHeadersOnly_ReturnsNoDataAndMarksJobFailed()
    {
        var fixture = new ImportFixture();
        ImportJob? job = null;
        fixture.JobRepository.Setup(repository => repository.AddAsync(It.IsAny<ImportJob>()))
            .Callback<ImportJob>(value => job = value).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(
            new ImportProductsCommand(ExcelFile(includeData: false)), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        job!.Status.Should().Be(ImportJobStatus.Failed);
        job.Errors.Should().ContainSingle(error => error.Message.Contains("không có dữ liệu"));
    }

    [Fact]
    public async Task Handle_WhenImportedRowsWouldExceedProductQuota_ReturnsRateLimit()
    {
        var fixture = new ImportFixture();
        fixture.Quota.MaxProductAllowed = 1;
        fixture.ProductRepository.Setup(repository => repository.CountAsync(It.IsAny<Expression<Func<Product, bool>>>() ))
            .ReturnsAsync(1);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.ProductRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<Product>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLatestQuotaDisappears_MarksJobFailed()
    {
        var fixture = new ImportFixture();
        fixture.QuotaRepository.SetupSequence(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync(fixture.Quota)
            .ReturnsAsync((BusinessQuota?)null);
        ImportJob? job = null;
        fixture.JobRepository.Setup(repository => repository.AddAsync(It.IsAny<ImportJob>()))
            .Callback<ImportJob>(value => job = value).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        job!.Status.Should().Be(ImportJobStatus.Failed);
    }

    [Fact]
    public async Task Handle_WhenLatestQuotaCannotCoverAllValidRows_ReturnsTooManyRequests()
    {
        var fixture = new ImportFixture();
        var reducedQuota = TestData.Quota(fixture.Business);
        reducedQuota.TokenLimit = 3_499;
        fixture.QuotaRepository.SetupSequence(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync(fixture.Quota)
            .ReturnsAsync(reducedQuota);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(429);
        fixture.ProductRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<Product>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OneValidAndOneInvalidRow_CompletesWithErrorsAndImportsOnlyValidProduct()
    {
        var fixture = new ImportFixture();
        IReadOnlyList<Product>? products = null;
        fixture.ProductRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<Product>>()))
            .Callback<IEnumerable<Product>>(values => products = values.ToList()).Returns(Task.CompletedTask);
        var file = ExcelFile(rows:
        [
            ValidRow("SKU-001"),
            ValidRow("SKU-002", price: "not-a-number")
        ]);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(file), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(ImportJobStatus.CompletedWithErrors);
        result.Data.SuccessRows.Should().Be(1);
        result.Data.FailedRows.Should().Be(1);
        result.Data.Errors.Should().Contain(error => error.Field == "Variant Price");
        products.Should().ContainSingle(product => product.ExternalId == "SKU-001");
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<ProductCreateEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidWorkbook_ImportsProductMetadataImageAndPublishesEvent()
    {
        var fixture = new ImportFixture();
        Product? product = null;
        fixture.ProductRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<Product>>()))
            .Callback<IEnumerable<Product>>(values => product = values.Single()).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(ImportJobStatus.Completed);
        product!.ExternalId.Should().Be("SKU-001");
        product.Status.Should().Be(ProductStatus.PendingEmbedding);
        product.Metadata.Should().Contain("weight_grams", "1200").And.Contain("tags", "gaming");
        product.Metadata.Should().Contain("color", "black");
        product.Images.Should().ContainSingle("https://shop.example/laptop.png");
        product.SearchContent.Should().Contain("Laptop").And.Contain("color: black");
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.Is<ProductCreateEvent>(message => message.ProductId == product.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBulkInsertThrows_MarksJobFailedAndReturnsServerFailure()
    {
        var fixture = new ImportFixture();
        fixture.ProductRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<Product>>()))
            .ThrowsAsync(new InvalidOperationException("bulk insert failed"));
        ImportJob? job = null;
        fixture.JobRepository.Setup(repository => repository.AddAsync(It.IsAny<ImportJob>()))
            .Callback<ImportJob>(value => job = value).Returns(Task.CompletedTask);

        var result = await fixture.Handler.Handle(new ImportProductsCommand(ExcelFile()), CancellationToken.None);

        result.StatusCode.Should().Be(500);
        job!.Status.Should().Be(ImportJobStatus.Failed);
        fixture.Publisher.Verify(endpoint => endpoint.Publish(
            It.IsAny<ProductCreateEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly List<string> RequiredHeaders =
    [
        "Title", "Body (HTML)", "Vendor", "Product Category", "Variant SKU",
        "Variant Price", "Variant Inventory Qty", "Variant Grams", "Tags",
        "Variant Image", "Option1 Name", "Option1 Value"
    ];

    private static Dictionary<string, string> ValidRow(string sku, string price = "25000000") => new()
    {
        ["Title"] = "Laptop",
        ["Body (HTML)"] = "Gaming laptop",
        ["Vendor"] = "Brand A",
        ["Product Category"] = "Laptop",
        ["Variant SKU"] = sku,
        ["Variant Price"] = price,
        ["Variant Inventory Qty"] = "5",
        ["Variant Grams"] = "1200",
        ["Tags"] = "gaming",
        ["Variant Image"] = "https://shop.example/laptop.png",
        ["Option1 Name"] = "color",
        ["Option1 Value"] = "black"
    };

    private static IFormFile ExcelFile(
        Action<List<string>>? mutateHeaders = null,
        bool includeData = true,
        IEnumerable<string>? extraHeaders = null,
        IReadOnlyList<Dictionary<string, string>>? rows = null,
        Action<List<string>>? headers = null)
    {
        var headerList = RequiredHeaders.ToList();
        mutateHeaders?.Invoke(headerList);
        headers?.Invoke(headerList);
        if (extraHeaders is not null) headerList.AddRange(extraHeaders);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");
        for (var index = 0; index < headerList.Count; index++)
            sheet.Cell(1, index + 1).Value = headerList[index];
        if (includeData)
        {
            var dataRows = rows ?? [ValidRow("SKU-001")];
            for (var rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < headerList.Count; columnIndex++)
                {
                    var header = headerList[columnIndex];
                    if (dataRows[rowIndex].TryGetValue(header, out var value))
                        sheet.Cell(rowIndex + 2, columnIndex + 1).Value = value;
                }
            }
        }
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        var bytes = output.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "products.xlsx");
    }

    private sealed class ImportFixture
    {
        public Business Business { get; } = TestData.Business();
        public BusinessQuota Quota { get; }
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IImportJobRepository> JobRepository { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IProductRepository> ProductRepository { get; } = new();
        public Mock<IBusinessQuotaRepository> QuotaRepository { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public ImportProductsCommandHandler Handler { get; }

        public ImportFixture()
        {
            Quota = TestData.Quota(Business);
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            QuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(Business.Id)).ReturnsAsync(Quota);
            ProductRepository.Setup(repository => repository.CountAsync(It.IsAny<Expression<Func<Product, bool>>>() ))
                .ReturnsAsync(0);
            ProductRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<Product, bool>>>(),
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>()))
                .ReturnsAsync([]);
            Handler = new ImportProductsCommandHandler(
                CurrentUser.Object, JobRepository.Object, UnitOfWork.Object,
                ProductRepository.Object, QuotaRepository.Object,
                new FixedTimeProvider(TestData.Now), TestData.Mapper(), Publisher.Object,
                Mock.Of<ILogger<ImportProductsCommandHandler>>());
        }
    }
}
