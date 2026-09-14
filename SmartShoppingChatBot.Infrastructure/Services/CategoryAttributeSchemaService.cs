using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using StackExchange.Redis;

namespace SmartShoppingChatBot.Infrastructure.Services;

public sealed class CategoryAttributeSchemaService : ICategoryAttributeSchemaService
{
    private const string KeyPrefix = "category-attribute-schema:active";
    private readonly IDatabase _database;
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly RedisOptions _options;
    private readonly ILogger<CategoryAttributeSchemaService> _logger;

    public CategoryAttributeSchemaService(
        IConnectionMultiplexer connectionMultiplexer,
        ICategoryAttributeSchemaRepository repository,
        IOptions<RedisOptions> options,
        ILogger<CategoryAttributeSchemaService> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CategoryAttributeSchema?> GetActiveSchemaAsync(string category)
    {
        var normalizedCategory = NormalizeCategory(category);
        var key = BuildKey(normalizedCategory);

        try
        {
            var cachedValue = await _database.StringGetAsync(key);
            if (cachedValue.HasValue)
            {
                try
                {
                    return BsonSerializer.Deserialize<CategoryAttributeSchema>(cachedValue.ToString());
                }
                catch (Exception exception) when (exception is FormatException or BsonSerializationException)
                {
                    _logger.LogWarning(exception, "Invalid cached category attribute schema for {Category}.", normalizedCategory);
                    await TryDeleteAsync(key);
                }
            }
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Could not read category attribute schema cache for {Category}.", normalizedCategory);
        }

        var schema = await _repository.GetActiveSchemaAsync(normalizedCategory);
        if (schema is null)
        {
            return null;
        }

        try
        {
            await _database.StringSetAsync(
                key,
                schema.ToJson(),
                TimeSpan.FromHours(_options.CategoryAttributeSchemaTtlHours));
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Could not cache active category attribute schema for {Category}.", normalizedCategory);
        }

        return schema;
    }

    public Task InvalidateAsync(string category)
    {
        return TryDeleteAsync(BuildKey(NormalizeCategory(category)));
    }

    private async Task TryDeleteAsync(RedisKey key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Could not invalidate category attribute schema cache key {CacheKey}.", key);
        }
    }

    private static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category cannot be empty.", nameof(category));
        }

        return category.Trim();
    }

    private static RedisKey BuildKey(string category)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(category));
        return $"{KeyPrefix}:{Convert.ToHexString(hash)}";
    }
}
