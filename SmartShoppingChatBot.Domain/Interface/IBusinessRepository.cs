using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IBusinessRepository : IGenericRepository<Business>
    {
        Task<Business?> GetByEmailAsync(string email);
        Task<Business?> GetByHotlineAsync(string hotline);
    }
}
