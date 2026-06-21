using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class SystemContentRepository : GenericRepository<SystemContent>, ISystemContentRepository
    {
        public SystemContentRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
