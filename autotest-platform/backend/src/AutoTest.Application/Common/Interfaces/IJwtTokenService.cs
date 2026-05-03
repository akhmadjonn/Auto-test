using AutoTest.Domain.Entities;

namespace AutoTest.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(User user, CancellationToken ct = default);
    Task<(Guid UserId, Guid SessionId)?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}
