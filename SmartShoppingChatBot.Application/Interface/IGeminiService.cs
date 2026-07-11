using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IGeminiService
    {
        Task<Result<string>> GenerateTextAsync(string prompt, int maxTokens, double temperature);

        Task<Result<double[]>> EmbeddingsAsync(string text, string taskType = "RETRIEVAL_QUERY");
    }
}
