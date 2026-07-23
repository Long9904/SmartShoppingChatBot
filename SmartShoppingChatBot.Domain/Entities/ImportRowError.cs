namespace SmartShoppingChatBot.Domain.Entities
{
    public class ImportRowError
    {
        public int RowNumber { get; set; }

        public string Field { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
