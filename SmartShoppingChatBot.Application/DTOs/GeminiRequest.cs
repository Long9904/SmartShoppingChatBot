namespace SmartShoppingChatBot.Application.DTOs
{
    public class GeminiRequest
    {
        public required string Prompt { get; set; }
        public string? SystemPrompt { get; set; }
        public GenerationConfig GenerationConfig { get; set; } = default!;
    }

    public class GenerationConfig
    {
        public double Temperature { get; set; } = 0.7;
        public int MaxOutputTokens { get; set; } = 9000;
    }
}
