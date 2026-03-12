using System.Collections.Concurrent;
using AutoTest.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class SystemSettingsService : ISystemSettingsService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICacheService _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SystemSettingsService> _logger;
    private readonly ConcurrentDictionary<string, string> _memoryCache = new();
    private const string RedisKeyPrefix = "avtolider:settings:";
    private const string PubSubChannel = "avtolider:settings:invalidate";
    private static readonly TimeSpan RedisTtl = TimeSpan.FromDays(1);
    private ISubscriber? _subscriber;

    public SystemSettingsService(
        IServiceScopeFactory scopeFactory,
        ICacheService cache,
        IConnectionMultiplexer redis,
        ILogger<SystemSettingsService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _redis = redis;
        _logger = logger;
        SubscribeToPubSub();
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        // 1. In-memory
        if (_memoryCache.TryGetValue(key, out var cached))
            return cached;

        // 2. Redis
        var redisValue = await _cache.GetAsync<string>($"{RedisKeyPrefix}{key}", ct);
        if (redisValue is not null)
        {
            _memoryCache[key] = redisValue;
            return redisValue;
        }

        // 3. Database (fallback)
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var setting = await db.SystemSettings.FindAsync([key], ct);
        if (setting is null)
            return null;

        _memoryCache[key] = setting.Value;
        await _cache.SetAsync($"{RedisKeyPrefix}{key}", setting.Value, RedisTtl, ct);
        return setting.Value;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var val = await GetAsync(key, ct);
        return int.TryParse(val, out var result) ? result : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var val = await GetAsync(key, ct);
        return bool.TryParse(val, out var result) ? result : fallback;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _memoryCache[key] = value;
        await _cache.SetAsync($"{RedisKeyPrefix}{key}", value, RedisTtl, ct);

        // Publish invalidation to all instances
        var subscriber = _redis.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(PubSubChannel), key);
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        if (!_memoryCache.IsEmpty)
            return new Dictionary<string, string>(_memoryCache);

        await ReloadFromDatabaseAsync(ct);
        return new Dictionary<string, string>(_memoryCache);
    }

    public async Task ReloadFromDatabaseAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var settings = await db.SystemSettings.AsNoTracking().ToListAsync(ct);

        _memoryCache.Clear();
        foreach (var s in settings)
        {
            _memoryCache[s.Key] = s.Value;
            await _cache.SetAsync($"{RedisKeyPrefix}{s.Key}", s.Value, RedisTtl, ct);
        }

        _logger.LogInformation("Reloaded {Count} system settings into memory+Redis", settings.Count);
    }

    private void SubscribeToPubSub()
    {
        try
        {
            _subscriber = _redis.GetSubscriber();
            var channel = RedisChannel.Literal(PubSubChannel);
            _subscriber.Subscribe(channel).OnMessage(channelMessage =>
            {
                var keyStr = channelMessage.Message.ToString();
                _memoryCache.TryRemove(keyStr, out _);
                _logger.LogDebug("Settings cache invalidated for key: {Key}", keyStr);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe to settings Pub/Sub channel");
        }
    }

    public void Dispose()
    {
        _subscriber?.UnsubscribeAll();
    }
}
