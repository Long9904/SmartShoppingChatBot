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

        Task<Result<double[]>> EmbeddingsAsyncV2(
            string text,
            string taskType = "RETRIEVAL_QUERY",
            CancellationToken ct = default);

        Task<Result<IReadOnlyList<double[]>>> EmbeddingsAsyncV3(
            IReadOnlyList<string> texts,
            string taskType = "RETRIEVAL_QUERY",
            CancellationToken ct = default);

        Task<Result<string>> GenerateTextAsyncV2(GeminiRequest geminiRequest, CancellationToken ct = default);


        Task<Result<ICollection<RankedRecord>>> RerankerAsyncV2(
            string userQuery,
            IEnumerable<RankRecord> records,
            CancellationToken ct);
    }
}
