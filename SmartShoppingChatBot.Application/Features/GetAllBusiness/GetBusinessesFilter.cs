using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.GetAllBusiness;

public class GetBusinessesFilter : QueryBase
{
    public string? Search { get; set; }
    
    public BusinessEnums? Status { get; set; }

    public DateTimeOffset? CreatedFrom { get; set; }
}
