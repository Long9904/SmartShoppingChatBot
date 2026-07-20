namespace SmartShoppingChatBot.Application.Commons.Options
{
    public sealed class RedisOptions
    {
        public const string SectionName = "Redis";

        public string ConnectionString { get; init; } = string.Empty;

        public int ConversationContextTtlHours { get; init; } = 8;

        public int RecentTurnLimit { get; init; } = 8;
    }
}
