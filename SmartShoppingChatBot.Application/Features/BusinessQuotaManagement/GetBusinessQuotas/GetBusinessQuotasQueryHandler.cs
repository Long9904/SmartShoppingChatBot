using MediatR;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;

public class GetBusinessQuotasQueryHandler
    : IRequestHandler<GetBusinessQuotasQuery, Result<BasePaginatedList<UsageQuotaLogResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessQuotaRepository _businessQuotaRepository;
    private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;

    public GetBusinessQuotasQueryHandler(
        ICurrentUserService currentUserService,
        IBusinessQuotaRepository businessQuotaRepository,
        IUsageQuotaLogRepository usageQuotaLogRepository)
    {
        _currentUserService = currentUserService;
        _businessQuotaRepository = businessQuotaRepository;
        _usageQuotaLogRepository = usageQuotaLogRepository;
    }

    public async Task<Result<BasePaginatedList<UsageQuotaLogResponse>>> Handle(
        GetBusinessQuotasQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data == null)
        {
            return Result<BasePaginatedList<UsageQuotaLogResponse>>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var currentQuota = await _businessQuotaRepository.GetCurrentBusinessQuota(
            businessResult.Data.Id);
        if (currentQuota == null)
        {
            return Result<BasePaginatedList<UsageQuotaLogResponse>>.Failure(
                404,
                "Business quota not found.",
                messageCode: BusinessQuotaMessageCode.NotFound);
        }

        var filter = request.Filter;
        var query = _usageQuotaLogRepository.AsQueryable()
            .Where(log =>
                log.BusinessId == businessResult.Data.Id
                && log.BusinessQuotaId == currentQuota.Id);

        if (filter.SourceType.HasValue)
        {
            query = query.Where(log => log.SourceType == filter.SourceType.Value);
        }

        query = ApplyLogOrder(query, filter.OrderBy);

        var page = await _usageQuotaLogRepository.PaginatedListAsync(
            query,
            filter.PageIndex,
            filter.PageSize);

        var response = new BasePaginatedList<UsageQuotaLogResponse>(
            page.Items.Select(ToResponse).ToList(),
            page.TotalItems,
            page.PageIndex,
            page.PageSize);

        return Result<BasePaginatedList<UsageQuotaLogResponse>>.Success(
            response,
            200,
            "Usage quota logs retrieved successfully.");
    }

    private static IQueryable<UsageQuotaLog> ApplyLogOrder(
        IQueryable<UsageQuotaLog> logs,
        string orderBy)
    {
        return orderBy.Trim().ToLowerInvariant() switch
        {
            "inputtokens" or "inputtokens asc" => logs.OrderBy(log => log.InputTokens),
            "inputtokens desc" => logs.OrderByDescending(log => log.InputTokens),
            "outputtokens" or "outputtokens asc" => logs.OrderBy(log => log.OutputTokens),
            "outputtokens desc" => logs.OrderByDescending(log => log.OutputTokens),
            "createdat" or "createdat asc" => logs.OrderBy(log => log.CreatedAt),
            _ => logs.OrderByDescending(log => log.CreatedAt)
        };
    }

    private static UsageQuotaLogResponse ToResponse(UsageQuotaLog log)
    {
        return new UsageQuotaLogResponse
        {
            Id = log.Id.ToString(),
            BusinessQuotaId = log.BusinessQuotaId.ToString(),
            SourceId = log.SourceId.ToString(),
            SourceType = log.SourceType,
            InputTokens = log.InputTokens,
            OutputTokens = log.OutputTokens,
            BillableTokens = log.BillableTokens,
            MessageUsed = log.MessageUsed,
            CreatedAt = log.CreatedAt
        };
    }
}
