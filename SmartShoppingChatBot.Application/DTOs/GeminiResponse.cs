namespace SmartShoppingChatBot.Application.DTOs
{
    public sealed class GeminiResponse<T>
    {
        public required T Result { get; init; }

        public long InputTokens { get; init; }

        public long OutputTokens { get; init; }
    }
}
