using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.DashboardManagement.BusinessDashboard;

public sealed class BusinessDashboardQuery : IRequest<Result<BusinessDashboardResponse>>
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
