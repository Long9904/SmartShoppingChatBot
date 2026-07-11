using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Events
{
    public class ProductCreateEvent
    {
        public string ProductId { get; set; } = default!;

        public Guid QdrantPointId { get; set; }
    }
}
