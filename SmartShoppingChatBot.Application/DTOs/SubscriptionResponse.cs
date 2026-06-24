namespace SmartShoppingChatBot.Application.DTOs
{
    public class SubscriptionResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }
        public int MaxProductAllowed { get; set; }
    }
}
