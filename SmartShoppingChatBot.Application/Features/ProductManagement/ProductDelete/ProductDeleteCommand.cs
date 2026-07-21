using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductDelete;

public class ProductDeleteCommand : IRequest<Result<ProductResponse>>
{
    public ObjectId? ProductId { get; set; }

    public string? ExternalId { get; set; }
}
