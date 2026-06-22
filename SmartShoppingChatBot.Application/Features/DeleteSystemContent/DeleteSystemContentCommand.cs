using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.DeleteSystemContent;

public class DeleteSystemContentCommand : IRequest<Result<SystemContentResponse>>
{
    public ObjectId SystemContentId { get; set; }
}
