namespace SmartShoppingChatBot.Application.DTOs
{
    public sealed class RankResponse
    {
        public List<RankedRecord> Records { get; set; } = [];
    }

    public sealed class RankedRecord
    {
        public string Id { get; set; } = default!;

        public float Score { get; set; }

        public string? Title { get; set; }

        public string? Content { get; set; }
    }
}
