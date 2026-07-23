using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class KernelChatResult
    {
        [JsonPropertyName("answer")]
        public required string Answer { get; init; } = "";

        [JsonPropertyName("summary")]
        public required string Summary { get; init; } = "";

        [JsonPropertyName("ai_summary_content")]
        public string AISummaryContent { get; init; } = string.Empty;
    }
}
