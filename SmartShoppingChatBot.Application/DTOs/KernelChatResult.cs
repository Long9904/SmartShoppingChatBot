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

        [JsonPropertyName("interactionType")]
        [System.ComponentModel.Description(
            "Interaction classification. Use ProductComparison only when the answer actually compares at least two real products; " +
            "otherwise use ProductSearch, ProductDetail, DocumentSearch, or General.")]
        public required string InteractionType { get; init; }

        [JsonPropertyName("comparedProductIds")]
        [System.ComponentModel.Description(
            "Canonical product IDs directly compared in the answer, in comparison order. " +
            "Must be empty unless interactionType is ProductComparison.")]
        public required List<string> ComparedProductIds { get; init; }

        [JsonPropertyName("trendKeywords")]
        [System.ComponentModel.Description(
            "Short search-trend keyword phrases inferred from the user's current shopping intent, in relevance order.")]
        public List<string>? TrendKeywords { get; init; }

        [JsonIgnore]
        public long InputTokens { get; set; }

        [JsonIgnore]
        public long OutputTokens { get; set; }
    }
}
