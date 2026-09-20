using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IKernelChatService
    {
        Task<Result<KernelChatResult>> ChatAsync(KernelChatRequest request);

        Task<Result<CategoryValueSelectionResult>> SelectCategoryValuesAsync(
            string prompt,
            string systemPrompt,
            CancellationToken cancellationToken = default);
    }
}
