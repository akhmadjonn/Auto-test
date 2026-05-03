using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace AutoTest.Infrastructure.Services;

public class JwtTokenService(IConfiguration configuration, IConnectionMultiplexer redis) : IJwtTokenService
{
    private readonly IDatabase _db = redis.GetDatabase();

    // Must match the fallback in Program.cs AddJwtBearer — both must use the same key
    private string SecretKey => configuration["JwtSettings:SecretKey"] ?? "super-secret-key-for-development-only-min-32-chars";
    private string Issuer => configuration["JwtSettings:Issuer"] ?? "AutoTest";
    private string Audience => configuration["JwtSettings:Audience"] ?? "AutoTest";
    private int AccessTokenMinutes => int.TryParse(configuration["JwtSettings:AccessTokenExpirationMinutes"], out var m) ? m : 15;
    private int RefreshTokenDays => int.TryParse(configuration["JwtSettings:RefreshTokenExpirationDays"], out var d) ? d : 30;

    public async Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(User user, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var ttl = TimeSpan.FromDays(RefreshTokenDays);

        await _db.StringSetAsync(RefreshKey(refreshToken), $"{user.Id}|{sessionId}", ttl);
        await _db.StringSetAsync(SessionKey(user.Id, sessionId), refreshToken, ttl);

        return (GenerateAccessToken(user, sessionId), refreshToken);
    }

    public async Task<(Guid UserId, Guid SessionId)?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(RefreshKey(refreshToken));
        if (value.IsNullOrEmpty)
            return null;

        var parts = value.ToString().Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId) || !Guid.TryParse(parts[1], out var sessionId))
            return null;

        return (userId, sessionId);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var sessionKey = SessionKey(userId, sessionId);
        var refreshToken = await _db.StringGetAsync(sessionKey);
        if (!refreshToken.IsNullOrEmpty)
            await _db.KeyDeleteAsync(RefreshKey(refreshToken!));
        await _db.KeyDeleteAsync(sessionKey);
    }

    private string GenerateAccessToken(User user, Guid sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone, user.PhoneNumber ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("preferred_language", user.PreferredLanguage.ToString()),
            new Claim(ClaimTypes.Sid, sessionId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string RefreshKey(string token) => $"avtolider:refresh:{token}";
    private static string SessionKey(Guid userId, Guid sessionId) => $"avtolider:session:{userId}:{sessionId}";
}
