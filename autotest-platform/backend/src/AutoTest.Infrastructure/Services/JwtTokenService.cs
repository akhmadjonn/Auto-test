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

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone, user.PhoneNumber ?? ""),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("preferred_language", user.PreferredLanguage.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var key = RefreshKey(token);
        await _db.StringSetAsync(key, userId.ToString(), TimeSpan.FromDays(RefreshTokenDays));
        return token;
    }

    public async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var key = RefreshKey(refreshToken);
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return null;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var key = RefreshKey(refreshToken);
        await _db.KeyDeleteAsync(key);
    }

    private static string RefreshKey(string token) => $"avtolider:refresh:{token}";
}
