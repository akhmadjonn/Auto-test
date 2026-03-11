using System.Text;
using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class EskizSmsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<EskizSmsService> logger) : ISmsService
{
    private string? _cachedToken;
    private DateTime _tokenExpiresAt;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        var client = httpClientFactory.CreateClient("Eskiz");

        var payload = new
        {
            mobile_phone = phoneNumber.TrimStart('+'),
            message,
            from = "4546",
            callback_url = ""
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "message/sms/send")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Eskiz SMS failed: {Status} {Body}", response.StatusCode, body);
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock (another thread may have refreshed already)
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            return await FetchTokenAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> FetchTokenAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("Eskiz");
        var email = configuration["EskizSettings:Email"] ?? "";
        var password = configuration["EskizSettings:Password"] ?? "";

        var payload = new { email, password };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("auth/login", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<EskizLoginResponse>(body, cancellationToken: ct);

        _cachedToken = result?.Data?.Token ?? throw new InvalidOperationException("Eskiz: token not returned");
        // Eskiz tokens are valid for 30 days; refresh 1h before expiry
        _tokenExpiresAt = DateTime.UtcNow.AddDays(29).AddHours(23);

        return _cachedToken;
    }

    private record EskizLoginResponse(EskizTokenData? Data);
    private record EskizTokenData(string Token);
}
