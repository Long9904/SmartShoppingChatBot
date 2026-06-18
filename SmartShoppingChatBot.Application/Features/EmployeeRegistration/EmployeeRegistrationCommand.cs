using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.EmployeeRegistration;

public class EmployeeRegistrationCommand : IRequest<Result<EmployeeRegistrationResponse>>
{
    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;
}
