using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberRegistration;

public class MemberRegistrationCommand : IRequest<Result<BusinessMemberRegistrationResponse>>
{
    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;
}
