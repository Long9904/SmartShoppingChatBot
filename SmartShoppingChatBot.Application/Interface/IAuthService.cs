using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IAuthService
    {
        Task<Result<Business?>> ValidateApiKeyAsync(string value);
    }
}
