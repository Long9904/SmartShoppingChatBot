using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using StackExchange.Redis;

namespace SmartShoppingChatBot.Infrastructure.Services;

public class RedisBusinessConfig : IRedisBusinessConfig
{
    private const string KeyPrefix = "business:config";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly IBusinessRepository _businessRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RedisBusinessConfig> _logger;

    public RedisBusinessConfig(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisOptions> options,
        IBusinessRepository businessRepository,
        ICurrentUserService currentUserService,
        ILogger<RedisBusinessConfig> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
        _currentUserService = currentUserService;
        _businessRepository = businessRepository;
        _logger = logger;
    }

    public async Task<BusinessConfig?> GetBusinessConfigAsync(CancellationToken cancellationToken = default)
    {
        var businessId = _currentUserService.GetBusinessId();
        if (businessId is null)
        {
            return null;
        }
        ValidateBusinessId(businessId);
        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(businessId);

        try
        {
            var value = await _database.StringGetAsync(key);

            if (!value.HasValue)
                return null;

            var config = JsonSerializer.Deserialize<BusinessConfig>(
                value.ToString(),
                JsonOptions);

            if (config is null)
            {
                _logger.LogWarning(
                    "Could not deserialize Redis context for business {businessId}",
                    businessId);

                await _database.KeyDeleteAsync(key);

                var business = await _businessRepository.FindAsync(b => b.Id == ObjectId.Parse(businessId));
                if (business is null) return new BusinessConfig
                {
                    FallBackMessage = string.Empty,
                    SystemPrompt = string.Empty,
                    MaxOutPutToken = 2000,
                    ModelTemperature = 0.2,
                    RerankingScore = 0.75,
                    TopKDocument = 3
                };

                return business.Config;
            }

            // Sliding expiration: mỗi lần sử dụng sẽ refresh TTL.
            await _database.KeyExpireAsync(key, GetExpiration());

            return config;
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not deserialize Redis context for business {businessId}",
                businessId);

            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not deserialize Redis context for business {businessId}",
                businessId);

            await TryDeleteAsync(key);

            return null;
        }
    }

    public async Task SetBusinessConfigAsync(Business business, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(business);


        cancellationToken.ThrowIfCancellationRequested();

        var key = BuildKey(business.Id.ToString());
        var json = JsonSerializer.Serialize(business.Config, JsonOptions);

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
                "Could not save business {businessId} to Redis",
                business.Id);

            // Redis chỉ là cache nên không throw tiếp.
            // Database vẫn là source of truth.
        }
    }




    private static RedisKey BuildKey(string businessId)
    {
        return $"{KeyPrefix}:{businessId}";
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

    private static void ValidateBusinessId(string businessId)
    {
        if (string.IsNullOrWhiteSpace(businessId))
        {
            throw new ArgumentException(
                "Business ID cannot be empty.",
                nameof(businessId));
        }

        if (!ObjectId.TryParse(businessId, out _))
        {
            throw new ArgumentException(
                "Business ID is invalid be empty.",
                nameof(businessId));
        }
    }
}
