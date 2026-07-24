using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.Auth.GetMyBusinessProfile;

public class GetMyBusinessProfileQuery : IRequest<Result<MyBusinessProfileResponse>>
{
}
