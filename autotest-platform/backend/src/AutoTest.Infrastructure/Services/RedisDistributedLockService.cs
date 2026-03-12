using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class RedisDistributedLockService(
    IConnectionMultiplexer redis,
    ILogger<RedisDistributedLockService> logger) : IDistributedLockService
{
    private readonly IDatabase _db = redis.GetDatabase();

    // Lua: only delete key if value matches (safe release — prevents releasing another owner's lock)
    private const string ReleaseLuaScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, TimeSpan expiry, CancellationToken ct = default)
    {
        var lockValue = Guid.NewGuid().ToString("N");
        var acquired = await _db.StringSetAsync(lockKey, lockValue, expiry, When.NotExists);

        if (!acquired)
            return null;

        logger.LogDebug("Lock acquired: {Key}", lockKey);
        return new RedisLockHandle(_db, lockKey, lockValue, logger);
    }

    public async Task<IAsyncDisposable> AcquireAsync(
        string lockKey, TimeSpan expiry, TimeSpan? retryTimeout = null, CancellationToken ct = default)
    {
        var timeout = retryTimeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow.Add(timeout);
        var delay = 50;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var handle = await TryAcquireAsync(lockKey, expiry, ct);
            if (handle is not null)
                return handle;

            await Task.Delay(delay, ct);
            delay = Math.Min(delay * 2, 200);
        }

        throw new TimeoutException($"Could not acquire lock '{lockKey}' within {timeout.TotalSeconds}s.");
    }

    private sealed class RedisLockHandle(
        IDatabase db, string key, string value, ILogger logger) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            try
            {
                await db.ScriptEvaluateAsync(ReleaseLuaScript, [(RedisKey)key], [(RedisValue)value]);
                logger.LogDebug("Lock released: {Key}", key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to release lock: {Key}", key);
            }
        }
    }
}
