namespace SmartShoppingChatBot.Application.DTOs
{
    public class RankRequest
    {
        public string Query { get; set; } = default!;

        public List<RankRecord> Records { get; set; } = [];

        public bool IgnoreRecordDetailsInResponse { get; set; }

        public string Model { get; set; } = "semantic-ranker-fast-004";
    }


    public class RankRecord
    {
        public string Id { get; set; } = default!;

        public string? Title { get; set; }

        public string? Content { get; set; }
    }
}
