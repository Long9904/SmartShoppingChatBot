using Microsoft.EntityFrameworkCore;
using SmartShoppingChatBot.Infrastructure;

namespace SmartShoppingChatBot.API.Extensions
{
    public static class MongoDbExtensions
    {
        public static IServiceCollection AddMongoDbConfig(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDb");
            var databaseName = configuration.GetValue<string>("DatabaseName");

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(databaseName))
            {
                throw new ArgumentNullException("Mongo db configuration is invalid");
            }
            
            services.AddDbContext<MongoDbContext>(options =>
            {
                options.UseMongoDB(connectionString, databaseName);
            });
            return services;
        }
    }
}
