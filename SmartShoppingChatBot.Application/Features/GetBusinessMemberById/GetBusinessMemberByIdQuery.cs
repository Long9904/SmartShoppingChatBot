using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.GetBusinessMemberById;

public class GetBusinessMemberByIdQuery : IRequest<Result<ProfileResponse>>
{
    public ObjectId MemberId { get; set; }
}
