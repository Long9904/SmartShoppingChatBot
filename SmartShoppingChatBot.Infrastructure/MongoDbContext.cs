using Microsoft.EntityFrameworkCore;

namespace SmartShoppingChatBot.Infrastructure;

public class MongoDbContext : DbContext
{
    // Data sets

    public MongoDbContext(DbContextOptions<MongoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
