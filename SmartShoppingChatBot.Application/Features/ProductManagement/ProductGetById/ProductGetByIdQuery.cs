using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetById;

public class ProductGetByIdQuery : IRequest<Result<ProductResponse>>
{
    public ObjectId? ProductId { get; set; }

    public string? ExternalId { get; set; }
}
