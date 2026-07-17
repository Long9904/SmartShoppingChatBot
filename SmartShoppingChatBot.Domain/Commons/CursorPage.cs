namespace SmartShoppingChatBot.Domain.Commons
{
    public class CursorPage<T>
    {
        public IReadOnlyList<T> Items { get; set; } = [];

        public bool HasMore { get; set; }

        public string? NextCursor { get; init; }
    }
}
