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
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string SenderName => configuration["EskizSettings:From"] ?? "4546";

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        var client = httpClientFactory.CreateClient("Eskiz");

        var request = new HttpRequestMessage(HttpMethod.Post, "message/sms/send")
        {
            Content = new FormUrlEncodedContent([
                new("mobile_phone", phoneNumber.TrimStart('+')),
                new("message", message),
                new("from", SenderName),
                new("callback_url", "")
            ])
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Eskiz SMS failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Eskiz SMS send failed: {response.StatusCode}");
        }

        // Parse response to log message ID and detect account issues
        try
        {
            var result = JsonSerializer.Deserialize<EskizSendResponse>(body, _jsonOptions);
            if (result?.Status is "error" or "failed")
            {
                logger.LogError("Eskiz SMS rejected: {Message}", result.Message);
                throw new InvalidOperationException($"Eskiz SMS rejected: {result.Message}");
            }

            logger.LogDebug("Eskiz SMS sent: id={Id} status={Status}", result?.Id, result?.Status);
        }
        catch (JsonException)
        {
            // Response parsed but not standard JSON — SMS was accepted (HTTP 200)
            logger.LogDebug("Eskiz SMS sent (unparsed response)");
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            // Try PATCH refresh if we have a token, fall back to full login
            if (_cachedToken is not null)
            {
                try
                {
                    return await RefreshTokenAsync(ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Eskiz token refresh failed, falling back to login");
                }
            }

            return await LoginAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> RefreshTokenAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("Eskiz");
        var request = new HttpRequestMessage(HttpMethod.Patch, "auth/refresh");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cachedToken);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var bodyStr = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<EskizLoginResponse>(bodyStr, _jsonOptions);

        _cachedToken = result?.Data?.Token ?? throw new InvalidOperationException("Eskiz: refresh returned no token");
        _tokenExpiresAt = DateTime.UtcNow.AddDays(29).AddHours(23);

        logger.LogInformation("Eskiz token refreshed via PATCH");
        return _cachedToken;
    }

    private async Task<string> LoginAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("Eskiz");
        var email = configuration["EskizSettings:Email"] ?? "";
        var password = configuration["EskizSettings:Password"] ?? "";

        var content = new FormUrlEncodedContent([
            new("email", email),
            new("password", password)
        ]);
        var response = await client.PostAsync("auth/login", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Eskiz login failed: {Status} {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var bodyStr = await response.Content.ReadAsStringAsync(ct);
        logger.LogDebug("Eskiz login response: {Body}", bodyStr);
        var result = JsonSerializer.Deserialize<EskizLoginResponse>(bodyStr, _jsonOptions);

        _cachedToken = result?.Data?.Token ?? throw new InvalidOperationException($"Eskiz: login returned no token. Response: {bodyStr}");
        _tokenExpiresAt = DateTime.UtcNow.AddDays(29).AddHours(23);

        logger.LogInformation("Eskiz token obtained via login");
        return _cachedToken;
    }

    // Called by EskizTokenRefreshService to proactively refresh token before expiry
    public async Task RefreshTokenIfNeededAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow.AddHours(24) < _tokenExpiresAt)
            return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow.AddHours(24) < _tokenExpiresAt)
                return;

            if (_cachedToken is not null)
            {
                try
                {
                    await RefreshTokenAsync(ct);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Eskiz proactive refresh failed, falling back to login");
                }
            }

            await LoginAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private record EskizLoginResponse(EskizTokenData? Data);
    private record EskizTokenData(string Token);

    private record EskizSendResponse(
        string? Id,
        string? Status,
        string? Message);
}
