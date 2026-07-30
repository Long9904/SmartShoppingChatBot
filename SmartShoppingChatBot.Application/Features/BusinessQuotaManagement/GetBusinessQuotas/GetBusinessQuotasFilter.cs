using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;

public class GetBusinessQuotasFilter : QueryBase
{
    public SourceTypeEnum? SourceType { get; set; }

    public string OrderBy { get; set; } = "CreatedAt desc";
}
