using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis GET failed for key {Key}, falling through to source", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json, expiry);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis SET failed for key {Key}, skipping cache write", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis DEL failed for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis EXISTS failed for key {Key}", key);
            return false;
        }
    }

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.StringIncrementAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis INCR failed for key {Key}", key);
            return 0;
        }
    }

    public async Task ExpireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyExpireAsync(key, expiry);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis EXPIRE failed for key {Key}", key);
        }
    }

    public async Task<TimeSpan?> GetTtlAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.KeyTimeToLiveAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis TTL failed for key {Key}", key);
            return null;
        }
    }
}
