using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class KnowledgeDocumentRepository : GenericRepository<KnowledgeDocument>, IKnowledgeDocumentRepository
    {
        public KnowledgeDocumentRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
