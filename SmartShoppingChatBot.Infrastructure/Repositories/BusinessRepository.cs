using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class BusinessRepository : GenericRepository<Business>, IBusinessRepository
    {

        public BusinessRepository(MongoDbContext context) : base(context)
        {
        }

        public async Task<Business?> GetByHotlineAsync(string hotline)
        {
            return await FindAsync(b => b.HotLine == hotline);
        }
    }
}
