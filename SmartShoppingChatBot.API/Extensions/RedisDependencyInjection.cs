using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using StackExchange.Redis;

namespace SmartShoppingChatBot.API.Extensions
{
    public static class RedisDependencyInjection
    {
        public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            services
                .AddOptions<RedisOptions>()
                .Bind(configuration.GetSection(RedisOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                    "Redis connection string is required.")
                .Validate(
                    options => options.RecentTurnLimit > 0,
                    "Recent turn limit must be greater than zero.")
                .Validate(
                    options => options.ConversationContextTtlHours > 0,
                    "Conversation context TTL must be greater than zero.")
                .ValidateOnStart();

            services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<RedisOptions>>()
                    .Value;

                var configurationOptions = ConfigurationOptions.Parse(
                    options.ConnectionString);

                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            });

            return services;
        }
    }
}
