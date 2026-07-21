using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using StackExchange.Redis;

namespace SmartShoppingChatBot.Infrastructure.Services;

public sealed class RedisConversationContextCacheService : IConversationContextCacheService
{
    private const string KeyPrefix = "conversation:context";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisConversationContextCacheService> _logger;

    public RedisConversationContextCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisConversationContextCacheService> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ConversationContextCache?> GetAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);

        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(conversationId);

        try
        {
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            var context = JsonSerializer.Deserialize<ConversationContextCache>(
                value.ToString(),
                JsonOptions);

            if (context is null)
            {
                _logger.LogWarning(
                    "Could not deserialize Redis context for conversation {ConversationId}",
                    conversationId);

                await _database.KeyDeleteAsync(key);

                return null;
            }

            // Sliding expiration: mỗi lần sử dụng sẽ refresh TTL.
            await _database.KeyExpireAsync(key, GetExpiration());

            return context;
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not read conversation context {ConversationId} from Redis",
                conversationId);

            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid conversation context JSON for {ConversationId}",
                conversationId);

            await TryDeleteAsync(key);

            return null;
        }
    }

    public async Task SetAsync(
        ConversationContextCache context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        ValidateConversationId(context.ConversationId);

        cancellationToken.ThrowIfCancellationRequested();

        context.UpdatedAt = DateTimeOffset.UtcNow;

        var key = BuildKey(context.ConversationId);
        var json = JsonSerializer.Serialize(context, JsonOptions);

        try
        {
            await _database.StringSetAsync(
                key,
                json,
                GetExpiration());
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not save conversation context {ConversationId} to Redis",
                context.ConversationId);

            // Redis chỉ là cache nên không throw tiếp.
            // Database vẫn là source of truth.
        }
    }

    public async Task<bool> RefreshExpirationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _database.KeyExpireAsync(
                BuildKey(conversationId),
                GetExpiration());
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not refresh Redis TTL for conversation {ConversationId}",
                conversationId);

            return false;
        }
    }

    public async Task RemoveAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ValidateConversationId(conversationId);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _database.KeyDeleteAsync(BuildKey(conversationId));
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove conversation context {ConversationId} from Redis",
                conversationId);
        }
    }

    private static RedisKey BuildKey(string conversationId)
    {
        return $"{KeyPrefix}:{conversationId}";
    }

    private TimeSpan GetExpiration()
    {
        return TimeSpan.FromHours(_options.ConversationContextTtlHours);
    }

    private async Task TryDeleteAsync(RedisKey key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (RedisException)
        {
            // Không cần làm request thất bại vì cache cleanup lỗi.
        }
    }

    private static void ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new ArgumentException(
                "Conversation ID cannot be empty.",
                nameof(conversationId));
        }
    }
}
