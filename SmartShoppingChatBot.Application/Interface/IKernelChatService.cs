using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IKernelChatService
    {
        Task<Result<KernelChatResult>> ChatAsync(KernelChatRequest request);
    }
}
