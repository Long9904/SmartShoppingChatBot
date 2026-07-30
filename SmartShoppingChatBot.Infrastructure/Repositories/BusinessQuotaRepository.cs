using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class BusinessQuotaRepository : GenericRepository<BusinessQuota>, IBusinessQuotaRepository
    {
        public BusinessQuotaRepository(MongoDbContext context) : base(context)
        {
        }

        public async Task<BusinessQuota?> GetCurrentBusinessQuota(ObjectId businessId)
        {
            var currentQuota = await _context.BusinessQuota
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.ResetDate > DateTimeOffset.UtcNow).
                    OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

            return currentQuota;
        }
    }
}
