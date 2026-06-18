using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface ICurrentUserService
    {
        Task<string?> GetUserId();
        Task<string?> GetBusinessId();

        Task<Result<User>> GetUser();
    }
}
