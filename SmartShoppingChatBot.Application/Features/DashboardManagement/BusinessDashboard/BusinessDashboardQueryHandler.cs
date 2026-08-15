using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.BusinessDashboard;

public sealed class BusinessDashboardQueryHandler(
    ICurrentUserService currentUserService,
    IProductRepository productRepository,
    IKnowledgeDocumentRepository knowledgeDocumentRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IConversationOrderRepository orderRepository,
    ISearchQueryLogRepository searchQueryLogRepository)
    : IRequestHandler<BusinessDashboardQuery, Result<BusinessDashboardResponse>>
{
    public async Task<Result<BusinessDashboardResponse>> Handle(
        BusinessDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<BusinessDashboardResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        if (!request.From.HasValue || !request.To.HasValue)
        {
            return Result<BusinessDashboardResponse>.Failure(
                400,
                "Both from and to dates are required.");
        }

        if (request.From.Value > request.To.Value)
        {
            return Result<BusinessDashboardResponse>.Failure(
                400,
                "The from date must be earlier than or equal to the to date.");
        }

        var businessId = businessResult.Data.Id;
        var fromVietnam = new DateTimeOffset(
            request.From.Value.ToDateTime(TimeOnly.MinValue),
            VietnamTimeOffset);
        var toVietnam = new DateTimeOffset(
            request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
            VietnamTimeOffset);
        var from = fromVietnam.ToUniversalTime();
        var to = toVietnam.ToUniversalTime();
        var range = request.To.Value.DayNumber - request.From.Value.DayNumber + 1;

        var products = productRepository.AsQueryable()
            .Where(product => product.BusinessId == businessId
                && product.Status != ProductStatus.Deleted
                && product.CreatedAt >= from
                && product.CreatedAt < to)
            .ToList();

        var documents = knowledgeDocumentRepository.AsQueryable()
            .Where(document => document.BusinessId == businessId
                && document.Status != KnowledgeDocumentStatus.Deleted
                && document.CreatedAt >= from
                && document.CreatedAt < to)
            .ToList();

        var conversations = conversationRepository.AsQueryable()
            .Where(conversation => conversation.BusinessId == businessId
                && conversation.CreateAt >= from
                && conversation.CreateAt < to)
            .ToList();

        var messages = messageRepository.AsQueryable()
            .Where(message => message.BusinessId == businessId
                && message.SenderType == SenderTypeEnum.Customer
                && message.CreatedAt >= from
                && message.CreatedAt < to)
            .ToList();

        var orders = orderRepository.AsQueryable()
            .Where(order => order.BusinessId == businessId
                && order.CreatedAt >= from
                && order.CreatedAt < to)
            .ToList();

        var searchLogs = searchQueryLogRepository.AsQueryable()
            .Where(log => log.BusinessId == businessId
                && log.CreatedAt >= from
                && log.CreatedAt < to)
            .ToList();

        var totalOrders = orders.Count;
        var paidOrders = orders.Count(order => order.Status == ConversationOrderEventStatus.Paid);
        var conversionRate = totalOrders == 0
            ? 0
            : (double)paidOrders / totalOrders * 100;

        var response = new BusinessDashboardResponse
        {
            From = fromVietnam,
            To = toVietnam,
            TotalProducts = products.Count,
            TotalKnowledgeDocuments = documents.Count,
            TotalChatSessions = conversations.Count,
            TotalChatMessages = messages.Count,
            TotalOrders = totalOrders,
            PaidOrders = paidOrders,
            ConversionRate = conversionRate,
            AverageRetrievalLatencyMilliseconds = searchLogs.Count == 0
                ? null
                : searchLogs.Average(log => (double)log.RetrievalLatency),
            AverageSearchHitRatePercentage = AverageSearchHitRatePercentage(searchLogs),
            ChatTraffic = BuildTraffic(messages, request.From.Value, range),
            ZeroResultQueries = BuildZeroResultQueries(searchLogs),
            Intents = BuildIntents(searchLogs),
            TrendingKeywords = BuildTrendingKeywords(searchLogs)
        };

        return Result<BusinessDashboardResponse>.Success(
            response,
            200,
            "Get business dashboard successfully.");
    }

    private static IReadOnlyList<BusinessDashboardTrafficPoint> BuildTraffic(
        IReadOnlyCollection<Message> messages,
        DateOnly from,
        int range)
    {
        var messagesByDate = messages
            .GroupBy(message => ToVietnamDate(message.CreatedAt))
            .ToDictionary(group => group.Key, group => group.ToList());

        return Enumerable.Range(0, range)
            .Select(offset =>
            {
                var date = from.AddDays(offset);
                messagesByDate.TryGetValue(date, out var dayMessages);
                return new BusinessDashboardTrafficPoint
                {
                    Date = date,
                    Sessions = dayMessages?.Select(message => message.ConversationId).Distinct().Count() ?? 0,
                    Messages = dayMessages?.Count ?? 0
                };
            })
            .ToList();
    }

    private static DateOnly ToVietnamDate(DateTimeOffset value)
        => DateOnly.FromDateTime(value.ToOffset(VietnamTimeOffset).DateTime);

    private static IReadOnlyList<BusinessDashboardZeroResultQuery> BuildZeroResultQueries(
        IReadOnlyCollection<SearchQueryLog> logs)
    {
        return logs
            .Where(log => log.ZeroResult && !string.IsNullOrWhiteSpace(log.UserRawQuery))
            .GroupBy(log => log.UserRawQuery!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new BusinessDashboardZeroResultQuery
            {
                Query = group.First().UserRawQuery!.Trim(),
                Count = group.Count(),
                LastOccurredAt = group.Max(log => log.CreatedAt)
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.LastOccurredAt)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<BusinessDashboardIntent> BuildIntents(
        IReadOnlyCollection<SearchQueryLog> logs)
    {
        return logs
            .GroupBy(log => string.IsNullOrWhiteSpace(log.InteractionType)
                ? "Unknown"
                : log.InteractionType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new BusinessDashboardIntent
            {
                Intent = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    private static IReadOnlyList<BusinessDashboardKeyword> BuildTrendingKeywords(
        IReadOnlyCollection<SearchQueryLog> logs)
    {
        return logs
            .SelectMany(log => log.TrendKeywords ?? [])
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .GroupBy(keyword => keyword, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BusinessDashboardKeyword
            {
                Keyword = group.First(),
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .Take(10)
            .ToList();
    }

    private static double? AverageSearchHitRatePercentage(IReadOnlyCollection<SearchQueryLog> logs)
    {
        var values = logs
            .Where(log => log.HitRateScore.HasValue)
            .Select(log => log.HitRateScore!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }

    private static readonly TimeSpan VietnamTimeOffset = TimeSpan.FromHours(7);
}
