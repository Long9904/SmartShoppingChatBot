using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.GetSystemContentById;

public class GetSystemContentByIdQuery : IRequest<Result<SystemContentResponse>>
{
    public ObjectId SystemContentId { get; set; }
}
