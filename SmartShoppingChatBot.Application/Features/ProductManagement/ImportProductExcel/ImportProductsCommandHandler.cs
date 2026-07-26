using System.Globalization;
using System.Text.RegularExpressions;
using AutoMapper;
using ClosedXML.Excel;
using MassTransit;
using MassTransit.Initializers;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ImportProductExcel;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, Result<ImportJobResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IImportJobRepository _importJobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductRepository _productRepository;
    private readonly IBusinessQuotaRepository _usinessQuotaRepository;
    private readonly TimeProvider _time;
    private readonly ILogger<ImportProductsCommandHandler> _logger;
    private readonly IPublishEndpoint _publisher;
    private readonly IMapper _mapper;

    public ImportProductsCommandHandler(
        ICurrentUserService currentUserService,
        IImportJobRepository importJobRepository,
        IUnitOfWork unitOfWork,
        IProductRepository productRepository,
        IBusinessQuotaRepository usinessQuotaRepository,
        TimeProvider time,
        IMapper mapper,
        IPublishEndpoint publishEndpoint,

        ILogger<ImportProductsCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _importJobRepository = importJobRepository;
        _unitOfWork = unitOfWork;
        _productRepository = productRepository;
        _usinessQuotaRepository = usinessQuotaRepository;
        _time = time;
        _publisher = publishEndpoint;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<ImportJobResponse>> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var business = await _currentUserService.GetBusiness();

        if (!business.IsSuccess)
            return Result<ImportJobResponse>.Failure(business.StatusCode, business.Message, business.Errors, business.MessageCode);

        // 1. Create new import job
        var importJob = new ImportJob
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = business.Data!.Id,
            FileName = request.File.FileName,
            Status = ImportJobStatus.Pending,
            TotalRows = 0,
            ProcessedRows = 0,
            SuccessRows = 0,
            FailedRows = 0,
            EmbeddedRows = 0,
            Errors = [],
            CreatedAt = _time.GetUtcNow(),
        };

        await _importJobRepository.AddAsync(importJob);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // CloseXML

        await using var stream = request.File.OpenReadStream();

        using var workbook = new XLWorkbook(stream);

        // 2. Check excel format
        if (!workbook.Worksheets.Any())
        {
            await MarkJobFailedAsync(
                importJob,
                "File Excel không có worksheet.",
                cancellationToken);

            return Result<ImportJobResponse>.Failure(
                400,
                "File Excel không có worksheet.", null, ImportJobMessageCode.FailExcelFormat);
        }

        var worksheet = workbook.Worksheet(1);

        // 3 Check header

        var headerResult = ValidateHeaders(worksheet);

        if (!headerResult.IsSuccess || headerResult.Data is null)
        {
            await MarkJobFailedAsync(
                importJob,
                headerResult.Message! ?? "Validate header fail",
                cancellationToken);

            return Result<ImportJobResponse>.Failure(
                headerResult.StatusCode,
                headerResult.Message,
                null,
                ImportJobMessageCode.FailExcelFormat);
        }



        // 4 Check data is mull ?
        var lastUsedRow = worksheet.LastRowUsed();

        if (lastUsedRow is null || lastUsedRow.RowNumber() <= 1)
        {
            await MarkJobFailedAsync(
                importJob,
                "File Excel không có dữ liệu sản phẩm.",
                cancellationToken);

            return Result<ImportJobResponse>.Failure(
                404,
                "File Excel không có dữ liệu sản phẩm.", null, ImportJobMessageCode.FailExcelData);
        }

        // 5 Check business quota
        var totalProductImport = lastUsedRow.RowNumber() - 1;

        var businessQuota = await _usinessQuotaRepository.FindAsync(b => b.BusinessId == business.Data.Id);
        if (businessQuota == null)
            return Result<ImportJobResponse>.Failure(404, "Business quota not found", null, BusinessQuotaMessageCode.NotFound);

        var productCount = await _productRepository.CountAsync(p => p.BusinessId == business.Data.Id && p.Status != ProductStatus.Deleted);

        if (productCount + totalProductImport > businessQuota.MaxProductAllowed)
        {
            return Result<ImportJobResponse>.Failure(400, "Rate limit for create new product", null, ProductMessageCode.ProdcutRateLimit);
        }

        importJob.TotalRows = totalProductImport;
        importJob.Status = ImportJobStatus.Validating;
        importJob.StartedAt = DateTimeOffset.UtcNow;
        await UpdateImportJob(importJob, cancellationToken);


        // 4 Check each row


        var headers = headerResult.Data.Headers;
        var optionPairs = headerResult.Data.OptionPairs;

        var errors = new List<ImportRowError>();
        var products = new List<Product>();
        var externalIdsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 4.1 Take externalId from SKU excel
        var externalIdsFromExcel = worksheet
            .RowsUsed()
            .Skip(1)
            .Select(row => row.Cell(headers["Variant SKU"]).GetString().Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 4.2 Take external ProductId from db and check is esixting
        var existingProducts = await _productRepository.FindAllAsync(p =>
            p.BusinessId == business.Data.Id
            && externalIdsFromExcel.Contains(p.ExternalId)
            && p.Status != ProductStatus.Deleted);

        var existingExternalIds = existingProducts
            .Select(x => x.ExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 4.3 Check duplicate external id in the excel file

        var importedExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Start form row 2
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var rowNumber = row.RowNumber();
            var rowHasError = false;

            var externalId = row.Cell(headers["Variant SKU"]).GetString().Trim();

            if (string.IsNullOrWhiteSpace(externalId))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant SKU",
                    Message = $"ExternalId trống"
                });

                rowHasError = true;
            }

            if (!string.IsNullOrWhiteSpace(externalId) && !importedExternalIds.Add(externalId))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant SKU",
                    Message = $"ExternalId '{externalId}' bị trùng trong file."
                });

                rowHasError = true;
            }

            if (!string.IsNullOrWhiteSpace(externalId) && existingExternalIds.Contains(externalId))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant SKU",
                    Message = $"ExternalId '{externalId}' đã tồn tại trong hệ thống."
                });

                rowHasError = true;
            }


            if (!string.IsNullOrWhiteSpace(externalId) && externalId.Length > 100)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant SKU",
                    Message = $"Độ dài ExternalId '{externalId}' lớn hơn 100"
                });

                rowHasError = true;
            }
            // Orther validate
            var productName = row.Cell(headers["Title"]).GetString().Trim();

            var description = row
                .Cell(headers["Body (HTML)"])
                .GetString()
                .Trim();

            var category = row
                .Cell(headers["Product Category"])
                .GetString()
                .Trim();

            var brand = row
                .Cell(headers["Vendor"])
                .GetString()
                .Trim();

            var priceText = row
                .Cell(headers["Variant Price"])
                .GetString()
                .Trim();

            var stockQuantityText = row
                .Cell(headers["Variant Inventory Qty"])
                .GetString()
                .Trim();

            decimal price = 0;
            int stockQuantity = 0;

            if (string.IsNullOrWhiteSpace(productName))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Title",
                    Message = "Name is required."
                });

                rowHasError = true;
            }
            else if (productName.Length > 200)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Title",
                    Message = "Name cannot exceed 200 characters."
                });

                rowHasError = true;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Body (HTML)",
                    Message = "Description is required."
                });

                rowHasError = true;
            }
            else if (description.Length > 500)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Body (HTML)",
                    Message = "Description cannot exceed 500 characters."
                });

                rowHasError = true;
            }

            // Price
            if (string.IsNullOrWhiteSpace(priceText))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Price",
                    Message = "Price is required."
                });

                rowHasError = true;
            }
            else if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out price))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Price",
                    Message = $"Price '{priceText}' không đúng định dạng số."
                });

                rowHasError = true;
            }
            else if (price < 0)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Price",
                    Message = "Price phải lớn hơn hoặc bằng 0."
                });

                rowHasError = true;
            }

            // Qty
            if (string.IsNullOrWhiteSpace(stockQuantityText))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Inventory Qty",
                    Message = "Stock quantity is required."
                });

                rowHasError = true;
            }
            else if (!int.TryParse(stockQuantityText, out stockQuantity))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Inventory Qty",
                    Message = $"Stock quantity '{stockQuantityText}' phải là số nguyên."
                });

                rowHasError = true;
            }
            else if (stockQuantity < 0)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Variant Inventory Qty",
                    Message = "Stock quantity phải lớn hơn hoặc bằng 0."
                });

                rowHasError = true;
            }


            // Category
            if (string.IsNullOrWhiteSpace(category))
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Product Category",
                    Message = "Category is required."
                });

                rowHasError = true;
            }
            else if (category.Length > 100)
            {
                errors.Add(new ImportRowError
                {
                    RowNumber = rowNumber,
                    Field = "Product Category",
                    Message = "Category cannot exceed 100 characters."
                });

                rowHasError = true;
            }
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var variantGrams = row
                .Cell(headers["Variant Grams"])
                .GetString()
                .Trim();

            var tags = row
                .Cell(headers["Tags"])
                .GetString()
                .Trim();

            if (!string.IsNullOrWhiteSpace(variantGrams))
            {
                metadata["weight_grams"] = variantGrams;
            }

            if (!string.IsNullOrWhiteSpace(tags))
            {
                metadata["tags"] = tags;
            }

            var images = new List<string>();

            var imageUrl = row
                .Cell(headers["Variant Image"])
                .GetString()
                .Trim();

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                if (imageUrl.Length > 500)
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = "Variant Image",
                        Message = "Image URL cannot exceed 500 characters."
                    });

                    rowHasError = true;
                }
                else
                {
                    images.Add(imageUrl);
                }
            }


            foreach (var pair in optionPairs)
            {
                var name = row.Cell(pair.NameColumn).GetString().Trim();
                var value = row.Cell(pair.ValueColumn).GetString().Trim();

                if (name.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("weight_grams", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Name",
                        Message = $"Metadata name '{name}' là tên dành riêng."
                    });

                    rowHasError = true;
                    continue;
                }

                // Check OptionX Name và ValueX Name có null không
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Name",
                        Message = "Tên metadata không được để trống."
                    });

                    rowHasError = true;
                    continue;
                }



                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Value",
                        Message = "Giá trị metadata không được để trống."
                    });

                    rowHasError = true;
                    continue;
                }

                if (name.Length > 20)
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Name",
                        Message = "Medata name không được vượt quá 20 kí tự"
                    });

                    rowHasError = true;
                    continue;
                }

                if (value.Length > 20)
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Value",
                        Message = "Medata value không được vượt quá 20 kí tự"
                    });

                    rowHasError = true;
                    continue;
                }

                // Try add = check contain + add
                if (!metadata.TryAdd(name, value))
                {
                    errors.Add(new ImportRowError
                    {
                        RowNumber = rowNumber,
                        Field = $"Option{pair.Index} Name",
                        Message = $"Tên metadata '{name}' bị trùng."
                    });

                    rowHasError = true;
                }

            }

            if (rowHasError)
            {
                continue;
            }
            var pointId = Guid.NewGuid();
            var product = new Product
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = business.Data.Id,
                QdrantPointId = pointId,
                ExternalId = externalId,
                ExternalProductUrl = "None",

                Name = productName,
                Description = description,
                Price = price,
                Currency = "VND",

                Brand = brand,

                StockQuantity = stockQuantity,

                Category = category,

                Status = ProductStatus.PendingEmbedding,

                Images = images,
                Metadata = metadata,

                CreatedAt = _time.GetUtcNow(),
                UpdatedAt = _time.GetUtcNow()

            };
            var embeddingText = product.BuildEmbeddingText();
            product.SearchContent = embeddingText;
            product.CreatedBy = new UserEmbedded
            {
                Name = "Business: " + business.Data.BusinessName,
            };

            product.UpdatedBy = new UserEmbedded
            {
                Name = "Business: " + business.Data.BusinessName,
            };

            products.Add(product);

            importJob.SuccessRows++;

        }
        importJob.FailedRows = errors
            .Select(x => x.RowNumber)
            .Distinct()
            .Count();

        importJob.SuccessRows = products.Count;
        importJob.Errors = errors;


        try
        {
            if (products.Count > 0)
            {
                importJob.Status = ImportJobStatus.ImportingProducts;

                await UpdateImportJob(importJob, cancellationToken);

                await _productRepository.AddRangeAsync(products);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new Exception("Import job failed");
            }
        }
        catch (Exception ex)
        {
            importJob.Status = ImportJobStatus.Failed;
            await UpdateImportJob(importJob, cancellationToken);
            _logger.LogError(ex, "Import job failed");
            return Result<ImportJobResponse>.Failure(500, "Import job failed", null, ImportJobMessageCode.FailExcelFormat);
        }

        importJob.Status = errors.Count == 0 ? ImportJobStatus.Completed : ImportJobStatus.CompletedWithErrors;
        importJob.CompletedAt = _time.GetUtcNow();

        await UpdateImportJob(importJob, cancellationToken);

        foreach (var product in products)
        {
            await _publisher.Publish(
                new ProductCreateEvent
                {
                    ProductId = product.Id.ToString(),
                    QdrantPointId = product.QdrantPointId,
                }, cancellationToken);

        }
        var response = _mapper.Map<ImportJobResponse>(importJob);

        _logger.LogInformation($"Job{importJob.Id} is import success with {importJob.Status} with {importJob.SuccessRows} SuccessRows and {importJob.FailedRows} Failed Rows");

        var messageCode = ImportJobMessageCode.SucessWithError;
        if (importJob.Status == ImportJobStatus.Completed)
        {
            messageCode = ImportJobMessageCode.Sucess;
        }

        return Result<ImportJobResponse>.Success(
            response,
            200,
            $"Job{importJob.Id} is import success with {importJob.Status} with {importJob.SuccessRows} SuccessRows and {importJob.FailedRows} Failed Rows",
            messageCode);
    }


    private async Task MarkJobFailedAsync(
        ImportJob importJob,
        string message,
        CancellationToken ct)
    {
        importJob.Status = ImportJobStatus.Failed;
        importJob.CompletedAt = DateTimeOffset.UtcNow;

        importJob.Errors.Add(new ImportRowError
        {
            RowNumber = 0,
            Field = "File",
            Message = message
        });

        await UpdateImportJob(importJob, ct);
    }

    private static Result<HeaderValidationResult> ValidateHeaders(IXLWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var lastColumn = worksheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var header = worksheet
                .Cell(1, column)
                .GetString()
                .Trim();

            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (headers.ContainsKey(header))
            {
                return Result<HeaderValidationResult>.Failure(
                    400,
                    $"Header '{header}' bị trùng.");
            }

            headers[header] = column;
        } // Lấy và check header có trùng không

        var requiredHeaders = new[]
        {
            "Title",
            "Body (HTML)",
            "Vendor",
            "Product Category",
            "Variant SKU",
            "Variant Price",
            "Variant Inventory Qty",
            "Product Category",
            "Variant Grams",
            "Tags"
        };

        // Check các cột default
        foreach (var requiredHeader in requiredHeaders)
        {
            if (!headers.ContainsKey(requiredHeader))
            {
                return Result<HeaderValidationResult>.Failure(
                    400,
                    $"Thiếu cột bắt buộc '{requiredHeader}'.");
            }
        }

        var optionIndexes = new HashSet<int>();

        // Lấy index số X của OptionX Name hoặc OptionX Value
        // ví dụ Option1 Name và Option1 Value thì chỉ có 1 (Hash Set)
        // Để ta xuy ra đc là có bao nhiêu Option

        foreach (var header in headers.Keys)
        {
            var match = Regex.Match(
                header,
                @"^Option(\d+) (Name|Value)$",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                optionIndexes.Add(int.Parse(match.Groups[1].Value));
            }
        }

        var optionPairs = new List<OptionColumnPair>();

        // Với mỗi Option, vd {1,2,4} thì ta check xem có Name1 và Value1 không

        foreach (var index in optionIndexes.OrderBy(x => x))
        {
            var nameHeader = $"Option{index} Name";
            var valueHeader = $"Option{index} Value";

            if (!headers.TryGetValue(nameHeader, out var nameColumn))
            {
                return Result<HeaderValidationResult>.Failure(
                    400,
                    $"Thiếu cột '{nameHeader}'.");
            }

            if (!headers.TryGetValue(valueHeader, out var valueColumn))
            {
                return Result<HeaderValidationResult>.Failure(
                    400,
                    $"Thiếu cột '{valueHeader}'.");
            }

            optionPairs.Add(new OptionColumnPair
            {
                Index = index,
                NameColumn = nameColumn,
                ValueColumn = valueColumn
            });
        }

        return Result<HeaderValidationResult>.Success(new HeaderValidationResult
        {
            Headers = headers,
            OptionPairs = optionPairs
        });
    }

    private async Task UpdateImportJob(ImportJob importJob, CancellationToken ct)
    {
        await _importJobRepository.UpdateAsync(importJob);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public class HeaderValidationResult
    {
        public Dictionary<string, int> Headers { get; set; } = [];

        public List<OptionColumnPair> OptionPairs { get; set; } = [];
    }

}
