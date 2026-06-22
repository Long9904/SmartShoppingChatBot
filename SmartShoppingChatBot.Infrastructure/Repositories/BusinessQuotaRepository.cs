using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class BusinessQuotaRepository : GenericRepository<BusinessQuota>, IBusinessQuotaRepository
    {
        public BusinessQuotaRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
