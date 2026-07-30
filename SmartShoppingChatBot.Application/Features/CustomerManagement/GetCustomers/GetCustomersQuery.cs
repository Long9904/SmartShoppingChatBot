using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;

public sealed class GetCustomersQuery
    : IRequest<Result<BasePaginatedList<CustomerListResponse>>>
{
    public GetCustomersFilter Filter { get; init; } = new();
}
