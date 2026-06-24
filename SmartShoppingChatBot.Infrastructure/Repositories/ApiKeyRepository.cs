using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class ApiKeyRepository : GenericRepository<ApiKey>, IApiKeyRepository
    {
        public ApiKeyRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
