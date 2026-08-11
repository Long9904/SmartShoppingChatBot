using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;

public sealed class GetCustomersFilter : QueryBase
{
    public string? CustomerExternalId { get; set; }

    public CustomerStatus? Status { get; set; }

    public string OrderBy { get; set; } = "CreatedAt desc";
}
