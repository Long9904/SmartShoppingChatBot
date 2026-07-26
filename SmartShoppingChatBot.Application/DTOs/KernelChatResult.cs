using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class KernelChatResult
    {
        [JsonPropertyName("answer")]
        public required string Answer { get; init; }

        [JsonPropertyName("summary")]
        public required string Summary { get; init; }

        [JsonPropertyName("ai_summary_content")]
        public required string AISummaryContent { get; init; }

        [JsonPropertyName("selectedProductIds")]
        [System.ComponentModel.Description("Product IDs actually shown or mentioned in answer, in the same order as answer.")]
        public required List<string> SelectedProductIds { get; init; }
    }
}
