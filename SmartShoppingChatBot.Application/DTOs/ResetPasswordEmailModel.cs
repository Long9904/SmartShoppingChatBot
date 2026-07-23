namespace SmartShoppingChatBot.Application.DTOs
{
    public class ResetPasswordEmailModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ResetPasswordUrl { get; set; } = string.Empty;
        public int ExpireMinutes { get; set; }
    }
}
