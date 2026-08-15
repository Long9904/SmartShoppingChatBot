using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories;

public sealed class ConversationOrderRepository(MongoDbContext context)
    : GenericRepository<ConversationOrder>(context), IConversationOrderRepository
{
}
