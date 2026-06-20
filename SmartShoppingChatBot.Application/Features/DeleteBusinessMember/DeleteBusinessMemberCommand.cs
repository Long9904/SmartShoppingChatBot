using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.DeleteBusinessMember;

public class DeleteBusinessMemberCommand : IRequest<Result<ProfileResponse>>
{
    public ObjectId MemberId { get; set; }
}
