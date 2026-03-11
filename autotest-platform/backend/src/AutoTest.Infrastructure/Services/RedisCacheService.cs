using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        await _db.KeyExistsAsync(key);

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default) =>
        await _db.StringIncrementAsync(key);

    public async Task ExpireAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
        await _db.KeyExpireAsync(key, expiry);

    public async Task<TimeSpan?> GetTtlAsync(string key, CancellationToken ct = default) =>
        await _db.KeyTimeToLiveAsync(key);
}
