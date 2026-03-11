namespace AutoTest.Application.Common.Interfaces;

public interface IOtpService
{
    Task<string> GenerateAndStoreAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken ct = default);
    Task<bool> IsRateLimitedAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool> IsOnCooldownAsync(string phoneNumber, CancellationToken ct = default);
}
