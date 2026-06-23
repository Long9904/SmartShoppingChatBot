using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.UserManagement.DeleteUser
{
    public class DeleteUserCommand : IRequest<Result<ProfileResponse>>
    {
        public string UserId { get; set; } = string.Empty;
    }
}
