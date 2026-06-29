namespace SmartShoppingChatBot.Application.Commons.Options
{
    public class QwenConfigs
    {
        public string ApiUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public int MaxTokens { get; set; }
    }
}
