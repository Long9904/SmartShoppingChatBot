using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreateEmbed
{
    public class ProductEmbedCommand : IRequest<Result<ProductResponse>>
    {
        public string ProductId { get; set; } = default!;

        public Guid QdrantPointId { get; set; } = default!;
    }
}
