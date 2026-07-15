namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProductSemanticSearchRequest
    {
        public string Query { get; init; } = string.Empty;

        public int CandidateLimit { get; init; } = 100;

        public int TopK { get; init; } = 10;

        public decimal? MinPrice { get; init; }

        public decimal? MaxPrice { get; init; }
    }
}
