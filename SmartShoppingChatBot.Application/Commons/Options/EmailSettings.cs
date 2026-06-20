namespace SmartShoppingChatBot.Application.Commons.Options
{
    public class EmailSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public bool UseSSL { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
        public string? FromName { get; set; }
    }
}
