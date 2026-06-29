using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IQwenService
    {
        Task<Result<string>> GenerateTextAsync(string prompt, int maxTokens, double temperature, bool enableThinking);
    }
}
