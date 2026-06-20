using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.GetMyBusinessProfile;

public class GetMyBusinessProfileQuery : IRequest<Result<BusinessResponse>>
{
}
