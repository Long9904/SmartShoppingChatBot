using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Domain.Interface
{
    public interface IBusinessQuotaRepository : IGenericRepository<BusinessQuota>
    {
        Task<BusinessQuota?> GetCurrentBusinessQuota(ObjectId businessId);
    }
}
