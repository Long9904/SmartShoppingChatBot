using MongoDB.Bson;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class SearchQueryLog
    {
        public ObjectId Id { get; set; }

        public ObjectId BusinessId { get; set; }

        public ObjectId ConversationId { get; set; }

        public ObjectId MessageId { get; set; }

        public string? UserRawQuery { get; set; }

        public List<string>? TrendKeywords { get; set; }

        public string? InteractionType { get; set; }

        public bool ZeroResult { get; set; }

        public int ResultCountNumber { get; set; }

        public int TopKResult { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public long RetrievalLatency { get; set; }

        public double? HitRateScore { get; set; }

        public List<ProductLogSnapshot> ProductResults { get; set; } = [];
    }

    public class ProductLogSnapshot
    {
        public ObjectId ProductId { get; set; }

        public string? ProductName { get; set; }

        public decimal Price { get; set; }

        public string? Category { get; set; }

        public double ProductScore { get; set; }
    }
}
