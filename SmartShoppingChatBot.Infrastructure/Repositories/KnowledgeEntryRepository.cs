using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class KnowledgeEntryRepository : GenericRepository<KnowledgeEntry>, IKnowledgeEntryRepository
    {
        public KnowledgeEntryRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
