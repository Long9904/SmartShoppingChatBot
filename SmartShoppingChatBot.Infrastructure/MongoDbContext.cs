using Microsoft.EntityFrameworkCore;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Infrastructure;

public class MongoDbContext : DbContext
{
    // Data sets
    public DbSet<Business> Businesses { get; set; }

    public DbSet<User> Users { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<BusinessSubscription> BusinessSubscriptions { get; set; }
    public DbSet<Payment> Payment { get; set; }
    public DbSet<BusinessQuota> BusinessQuota { get; set; }

    public DbSet<Token> Tokens { get; set; }

    public DbSet<SystemContent> SystemContents { get; set; }

    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
    public DbSet<KnowledgeEntry> KnowledgeEntries { get; set; }
    public DbSet<Product> Products { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Conversation> Conversations { get; set; }

    public DbSet<Message> Messages { get; set; }

    public DbSet<ImportJob> ImportJobs { get; set; }

    public MongoDbContext(DbContextOptions<MongoDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });
        modelBuilder.Entity<BusinessQuota>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BusinessSubscriptionId);
        });
        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<BusinessSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SystemContent>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.KeyId).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QdrantPointId).IsUnique();
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
        modelBuilder.Entity<KnowledgeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QdrantPointId).IsUnique();
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(e => e.Id);
            e.HasIndex(e => new { e.BusinessId, e.CustomerExternalId })
            .IsUnique();
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(e => e.Id);
            e.HasIndex(e => new { e.ConversationId, e.Id });
        });
    }
}
