using Microsoft.EntityFrameworkCore;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Infrastructure;

public class MongoDbContext : DbContext
{
    // Data sets
    public DbSet<Business> Businesses { get; set; }

    public MongoDbContext(DbContextOptions<MongoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Business>();
    }
}
