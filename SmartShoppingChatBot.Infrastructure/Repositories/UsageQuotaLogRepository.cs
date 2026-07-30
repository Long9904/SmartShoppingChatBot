using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class UsageQuotaLogRepository : GenericRepository<UsageQuotaLog>, IUsageQuotaLogRepository
    {
        public UsageQuotaLogRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
