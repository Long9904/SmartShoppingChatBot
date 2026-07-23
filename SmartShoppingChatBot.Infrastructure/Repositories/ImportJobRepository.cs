using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class ImportJobRepository : GenericRepository<ImportJob>, IImportJobRepository
    {
        public ImportJobRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
