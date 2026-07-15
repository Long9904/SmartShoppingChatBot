using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IGeminiService
    {
        Task<Result<string>> GenerateTextAsync(
            string prompt,
            int maxTokens,
            double temperature,
            string systemPrompt = "");

        Task<Result<double[]>> EmbeddingsAsync(string text, string taskType = "RETRIEVAL_QUERY");

        Task<Result<string>> GenerateTextAsyncV2(GeminiRequest geminiRequest);

        Task<Result<ICollection<RankedRecord>>> RerankerAsync(
            string userQuery,
            IEnumerable<RankRecord> records,
            CancellationToken ct);
    }
}
