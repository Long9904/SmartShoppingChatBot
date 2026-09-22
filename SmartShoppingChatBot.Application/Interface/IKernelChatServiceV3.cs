using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface;

public interface IKernelChatServiceV3
{
    Task<Result<KernelChatResultV3>> ChatAsync(KernelChatRequestV3 request);
}
