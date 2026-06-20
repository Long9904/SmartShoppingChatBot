using System.Threading.Tasks;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IBusinessRepository : IGenericRepository<Business>
    {
        Task<Business?> GetByHotlineAsync(string hotline);
    }
}
