using MongoDB.Bson;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class ProductComparation
    {
        public ObjectId Id { get; set; }

        public ObjectId ConversationId { get; set; }

        public ObjectId BusinessId { get; set; }

        public ObjectId MessageId { get; set; }

        public ObjectId CustomerId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string? Title { get; set; }

        public string? Summary { get; set; }

        public List<ProductSnapshot> RecommendationObjects { get; set; } = [];
    }


    public class ProductSnapshot
    {
        public ObjectId ProductId { get; set; }

        public string? ProductName { get; set; }

        public decimal Price { get; set; }

        public string? Category { get; set; }
    }
}
