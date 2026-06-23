using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.UserManagement.GetUserById
{
    public class GetUserByIdQuery : IRequest<Result<ProfileResponse>>   
    {
        public string UserId { get; set; } = string.Empty;
    }
}
