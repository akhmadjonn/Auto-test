using AutoTest.Application.Common.Interfaces;
using AutoTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

public class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

public class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsAdmin { get; set; }
}

public class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var val) && val is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_store.ContainsKey(key));
}

public class FakeFileStorageService : IFileStorageService
{
    public Task<string> UploadQuestionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default) =>
        Task.FromResult($"questions/{category}/{Guid.NewGuid()}.webp");

    public Task<string> UploadAnswerOptionImageAsync(Stream stream, string fileName, string category, CancellationToken ct = default) =>
        Task.FromResult($"questions/{category}/{Guid.NewGuid()}.webp");

    public Task<string> GetPresignedUrlAsync(string key, CancellationToken ct = default) =>
        Task.FromResult($"https://minio.local/{key}?signed=1");

    public Task<string> GetThumbnailUrlAsync(string key, CancellationToken ct = default) =>
        Task.FromResult($"https://minio.local/{key}_thumb?signed=1");

    public Task<Dictionary<string, string>> GetPresignedUrlsBatchAsync(IEnumerable<string> objectKeys, CancellationToken ct = default) =>
        Task.FromResult(objectKeys.ToDictionary(k => k, k => $"https://minio.local/{k}?signed=1"));

    public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteManyAsync(IEnumerable<string> keys, CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeDistributedLockService : IDistributedLockService
{
    public Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, TimeSpan expiry, CancellationToken ct = default) =>
        Task.FromResult<IAsyncDisposable?>(new FakeLockHandle());

    public Task<IAsyncDisposable> AcquireAsync(string lockKey, TimeSpan expiry, TimeSpan? retryTimeout = null, CancellationToken ct = default) =>
        Task.FromResult<IAsyncDisposable>(new FakeLockHandle());

    private sealed class FakeLockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
