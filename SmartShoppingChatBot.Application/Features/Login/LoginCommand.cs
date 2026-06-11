using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.Login;

public class LoginCommand : IRequest<Result<LoginResponse>>
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
