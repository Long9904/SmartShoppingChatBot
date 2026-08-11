using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;

public class GetBusinessQuotasQuery
    : IRequest<Result<BasePaginatedList<UsageQuotaLogResponse>>>
{
    public GetBusinessQuotasFilter Filter { get; set; } = new();
}
