using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
