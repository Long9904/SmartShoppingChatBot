namespace SmartShoppingChatBot.Application.Events
{
    public class DocumentUploadedEvent
    {
        public string DocumentId { get; set; } = string.Empty;
        public string BusinessId { get; set; } = string.Empty;
    }
}
