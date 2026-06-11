using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.SelectBusiness;

public class SelectBusinessCommand : IRequest<Result<SelectBusinessResponse>>
{
    public ObjectId BusinessId { get; set; }
}
