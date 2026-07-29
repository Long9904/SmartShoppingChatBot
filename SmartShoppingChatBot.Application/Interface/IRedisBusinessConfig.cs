using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IRedisBusinessConfig
    {
        Task<BusinessConfig?> GetBusinessConfigAsync(CancellationToken cancellationToken = default);

        Task SetBusinessConfigAsync(Business business, CancellationToken cancellationToken = default);
    }
}
