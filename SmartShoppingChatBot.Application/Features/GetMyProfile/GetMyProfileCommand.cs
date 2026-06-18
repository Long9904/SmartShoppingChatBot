using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.GetMyProfile
{
    public class GetMyProfileCommand : IRequest<Result<ProfileResponse>>
    {
    }
}
