using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Infrastructure.Repositories
{
    public class BusinessQuotaRepository : GenericRepository<BusinessQuota>, IBusinessQuotaRepository
    {
        public BusinessQuotaRepository(MongoDbContext context) : base(context)
        {
        }
    }
}
