namespace AutoTest.Application.Common.Interfaces;

public interface IDistributedLockService
{
    /// Returns null if lock is not acquired (non-blocking). Use `await using` for auto-release.
    Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, TimeSpan expiry, CancellationToken ct = default);

    /// Retries with backoff up to retryTimeout (default 5s). Throws TimeoutException if not acquired.
    Task<IAsyncDisposable> AcquireAsync(string lockKey, TimeSpan expiry, TimeSpan? retryTimeout = null, CancellationToken ct = default);
}
