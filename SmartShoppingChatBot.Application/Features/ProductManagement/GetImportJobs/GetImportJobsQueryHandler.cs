using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;

public class GetImportJobsQueryHandler
    : IRequestHandler<GetImportJobsQuery, Result<BasePaginatedList<ImportJobResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IImportJobRepository _importJobRepository;

    public GetImportJobsQueryHandler(
        ICurrentUserService currentUserService,
        IImportJobRepository importJobRepository)
    {
        _currentUserService = currentUserService;
        _importJobRepository = importJobRepository;
    }

    public async Task<Result<BasePaginatedList<ImportJobResponse>>> Handle(
        GetImportJobsQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data == null)
        {
            return Result<BasePaginatedList<ImportJobResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var filter = request.Filter;
        var query = _importJobRepository.AsQueryable()
            .Where(importJob => importJob.BusinessId == businessResult.Data.Id);

        if (!string.IsNullOrWhiteSpace(filter.FileName))
        {
            var fileName = filter.FileName.Trim();
            query = query.Where(importJob => importJob.FileName.Contains(fileName));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(importJob => importJob.Status == filter.Status.Value);
        }

        query = query.OrderByDescending(importJob => importJob.CreatedAt);

        var page = await _importJobRepository.PaginatedListAsync(
            query,
            filter.PageIndex,
            filter.PageSize);

        var response = new BasePaginatedList<ImportJobResponse>(
            page.Items.Select(importJob => new ImportJobResponse
            {
                Id = importJob.Id.ToString(),
                FileName = importJob.FileName,
                Status = importJob.Status,
                TotalRows = importJob.TotalRows,
                ProcessedRows = importJob.ProcessedRows,
                SuccessRows = importJob.SuccessRows,
                FailedRows = importJob.FailedRows,
                EmbeddedRows = importJob.EmbeddedRows,
                Errors = importJob.Errors.ToList(),
                CreatedAt = importJob.CreatedAt,
                StartedAt = importJob.StartedAt,
                CompletedAt = importJob.CompletedAt
            }).ToList(),
            page.TotalItems,
            page.PageIndex,
            page.PageSize);

        return Result<BasePaginatedList<ImportJobResponse>>.Success(
            response,
            200,
            "Import jobs retrieved successfully.",
            ImportJobMessageCode.Sucess);
    }
}
