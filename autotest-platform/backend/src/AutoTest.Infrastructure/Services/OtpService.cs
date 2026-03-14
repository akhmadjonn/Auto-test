using System.Security.Cryptography;
using System.Text;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class OtpService(IConnectionMultiplexer redis, IConfiguration configuration) : IOtpService
{
    private readonly IDatabase _db = redis.GetDatabase();

    private int OtpTtlMinutes => int.TryParse(configuration["OtpSettings:TtlMinutes"], out var v) ? v : 5;
    private int RateLimitCount => int.TryParse(configuration["OtpSettings:RateLimitCount"], out var v) ? v : 3;
    private int RateLimitWindowMinutes => int.TryParse(configuration["OtpSettings:RateLimitWindowMinutes"], out var v) ? v : 15;
    private int CooldownSeconds => int.TryParse(configuration["OtpSettings:CooldownSeconds"], out var v) ? v : 60;
    private int VerifyMaxAttempts => int.TryParse(configuration["OtpSettings:VerifyMaxAttempts"], out var v) ? v : 5;
    private int VerifyWindowMinutes => int.TryParse(configuration["OtpSettings:VerifyWindowMinutes"], out var v) ? v : 15;
    private string HmacSecret => configuration["OtpSettings:HmacSecret"] ?? "otp-hmac-secret-for-dev";

    public bool IsWhitelistedNumber(string phoneNumber) =>
        configuration[$"OtpSettings:WhitelistNumbers:{NormalizePhone(phoneNumber)}"] is not null;

    public string? GetWhitelistCode(string phoneNumber) =>
        configuration[$"OtpSettings:WhitelistNumbers:{NormalizePhone(phoneNumber)}"];

    public async Task<string> GenerateAndStoreAsync(string phoneNumber, CancellationToken ct = default)
    {
        // Whitelisted numbers get a fixed code, skip Redis/SMS
        var whitelistCode = GetWhitelistCode(phoneNumber);
        if (whitelistCode is not null)
        {
            var hash = ComputeHash(phoneNumber, whitelistCode);
            await _db.StringSetAsync(OtpKey(phoneNumber), hash, TimeSpan.FromMinutes(OtpTtlMinutes));
            return whitelistCode;
        }

        var code = GenerateCode();
        var codeHash = ComputeHash(phoneNumber, code);

        await _db.StringSetAsync(OtpKey(phoneNumber), codeHash, TimeSpan.FromMinutes(OtpTtlMinutes));

        // Record this send for rate limiting
        var countKey = RateLimitKey(phoneNumber);
        var count = await _db.StringIncrementAsync(countKey);
        if (count == 1)
            await _db.KeyExpireAsync(countKey, TimeSpan.FromMinutes(RateLimitWindowMinutes));

        // Set cooldown
        await _db.StringSetAsync(CooldownKey(phoneNumber), "1", TimeSpan.FromSeconds(CooldownSeconds));

        return code;
    }

    public async Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        var storedHash = await _db.StringGetAsync(OtpKey(phoneNumber));
        if (storedHash.IsNullOrEmpty)
            return false;

        var expectedHash = ComputeHash(phoneNumber, code);
        if (storedHash != expectedHash)
            return false;

        // Invalidate after successful verification
        await _db.KeyDeleteAsync(OtpKey(phoneNumber));
        return true;
    }

    public async Task<bool> IsRateLimitedAsync(string phoneNumber, CancellationToken ct = default)
    {
        if (IsWhitelistedNumber(phoneNumber))
            return false;

        var countStr = await _db.StringGetAsync(RateLimitKey(phoneNumber));
        return countStr.HasValue && long.TryParse(countStr, out var count) && count >= RateLimitCount;
    }

    public async Task<bool> IsOnCooldownAsync(string phoneNumber, CancellationToken ct = default)
    {
        if (IsWhitelistedNumber(phoneNumber))
            return false;

        return await _db.KeyExistsAsync(CooldownKey(phoneNumber));
    }

    private static string GenerateCode() =>
        Random.Shared.Next(100_000, 999_999).ToString();

    private string ComputeHash(string phoneNumber, string code)
    {
        var data = Encoding.UTF8.GetBytes($"{phoneNumber}:{code}");
        var key = Encoding.UTF8.GetBytes(HmacSecret);
        return Convert.ToHexString(HMACSHA256.HashData(key, data));
    }

    public async Task<(bool Allowed, int Remaining)> CheckAndIncrementVerifyAttemptsAsync(
        string phoneNumber, CancellationToken ct = default)
    {
        if (IsWhitelistedNumber(phoneNumber))
            return (true, 999);

        var key = VerifyAttemptsKey(phoneNumber);
        var count = await _db.StringIncrementAsync(key);
        if (count == 1)
            await _db.KeyExpireAsync(key, TimeSpan.FromMinutes(VerifyWindowMinutes));

        var max = VerifyMaxAttempts;
        return (count <= max, (int)Math.Max(0, max - count));
    }

    public async Task ResetVerifyAttemptsAsync(string phoneNumber, CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(VerifyAttemptsKey(phoneNumber));

    private static string NormalizePhone(string phone) =>
        phone.TrimStart('+');

    private static string OtpKey(string phone) => $"avtolider:otp:{phone}";
    private static string RateLimitKey(string phone) => $"avtolider:otp:ratelimit:{phone}";
    private static string CooldownKey(string phone) => $"avtolider:otp:cooldown:{phone}";
    private static string VerifyAttemptsKey(string phone) => $"avtolider:otp:verify_attempts:{phone}";
}
