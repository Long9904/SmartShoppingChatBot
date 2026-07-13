using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Infrastructure.Seeders
{
    public class SubscriptionSeeder
    {
        private readonly MongoDbContext _context;
        private IConfiguration _configuration;
        private readonly ILogger<SubscriptionSeeder> _logger;

        public SubscriptionSeeder(MongoDbContext context, IConfiguration configuration, ILogger<SubscriptionSeeder> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedSubscriptionsAsync()
        {

            var subscriptionBasic = new SubscriptionPlan
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                Name = "Basic",
                Description = "Basic subscription plan with limited features.",
                Price = 0,
                Duration = 30,
                Level = 0,
                Status = StatusEnums.Active,
                TokenLimit = 15000000,
                MessageLimit = 3000,
                MaxProductAllowed = 100,
                MaxDocumentAllowed = 15,
            };
            var subscriptionPro = new SubscriptionPlan
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                Name = "Pro",
                Description = "Pro subscription plan with advanced features.",
                Price = 1990000,
                Duration = 30,
                Level = 1,
                Status = StatusEnums.Active,
                TokenLimit = 60000000,
                MessageLimit = 10000,
                MaxProductAllowed = 1000,
                MaxDocumentAllowed = 50,
            };
            var subscriptionEnterprise = new SubscriptionPlan
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                Name = "Enterprise",
                Description = "Enterprise subscription plan with all features.",
                Price = 4990000,
                Duration = 30,
                Level = 2,
                Status = StatusEnums.Active,
                TokenLimit = 4000000000,
                MessageLimit = 50000,
                MaxProductAllowed = int.MaxValue,
                MaxDocumentAllowed = 100,
            };
            var existingSubscriptions = await _context.SubscriptionPlans.FirstOrDefaultAsync(u => u.Name == subscriptionBasic.Name || u.Name == subscriptionPro.Name || u.Name == subscriptionEnterprise.Name);
            if (existingSubscriptions != null)
            {
                _logger.LogInformation("Subscription plans already exist in the database. Skipping seeding.");
                return;
            }
            await _context.SubscriptionPlans.AddRangeAsync(subscriptionBasic, subscriptionPro, subscriptionEnterprise);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Subscription plans seeded successfully.");

        }
    }
}
