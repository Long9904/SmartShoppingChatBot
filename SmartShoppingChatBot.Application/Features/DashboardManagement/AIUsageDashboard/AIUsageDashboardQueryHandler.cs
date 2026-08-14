using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.AIUsageDashboard
{
    public class AIUsageDashboardQueryHandler : IRequestHandler<AIUsageDashboardQuery, Result<AIUsageDashboardResponse>>
    {
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;

        public AIUsageDashboardQueryHandler(IUsageQuotaLogRepository usageQuotaLogRepository)
        {
            _usageQuotaLogRepository = usageQuotaLogRepository;
        }

        public Task<Result<AIUsageDashboardResponse>> Handle(AIUsageDashboardQuery request, CancellationToken cancellationToken)
        {
            try
            {
                // Get usage quota logs
                var usageLogs = _usageQuotaLogRepository.AsQueryable().ToList();

                // Calculate date range based on filter
                if(request.Filter.Range <= 0 )
                {
                    request.Filter.Range = 7; // Default to last 7 days if range is not provided or invalid
                }
                var endDate = DateTime.Now;
                var startDate = endDate.AddDays(-request.Filter.Range);

                // Filter logs by date range
                var logsInRange = usageLogs
                    .Where(l => l.CreatedAt.Date >= startDate.Date && l.CreatedAt.Date <= endDate.Date)
                    .ToList();

                // Calculate totals
                var totalInputTokens = logsInRange.Sum(l => l.InputTokens);
                var totalOutputTokens = logsInRange.Sum(l => l.OutputTokens);
                var totalTokenUsed = totalInputTokens + totalOutputTokens;
                var totalMessageUsed = logsInRange.Sum(l => l.MessageUsed);

                // Group by date for chart data
                var chartData = logsInRange
                    .GroupBy(l => l.CreatedAt.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new AIUsageDashboardChartResponse
                    {
                        Date = DateOnly.FromDateTime(g.Key),
                        TotalTokenUsed = (int)(g.Sum(l => l.InputTokens) + g.Sum(l => l.OutputTokens))
                    })
                    .ToList();

                // Create response
                var response = new AIUsageDashboardResponse
                {
                    TotalTokenUsed = (int)totalTokenUsed,
                    InputTokenUsed = (int)totalInputTokens,
                    OutputTokenUsed = (int)totalOutputTokens,
                    TotalMessageUsed = totalMessageUsed,
                    ChartData = chartData
                };

                return Task.FromResult(Result<AIUsageDashboardResponse>.Success(response));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<AIUsageDashboardResponse>.Failure(
                    500,
                    $"An error occurred while processing the AI usage dashboard: {ex.Message}"));
            }
        }
    }
}
