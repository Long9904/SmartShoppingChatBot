namespace SmartShoppingChatBot.Application.DTOs
{
    public class ConversationResponse
    {
        public string ConversationId { get; set; } = string.Empty;

        public string ConversationTitle { get; set; } = string.Empty;


        public required string MessageResponse { get; set; }
    }
}
