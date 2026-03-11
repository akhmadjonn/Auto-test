using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Auth;

public record RefreshTokenCommand(string RefreshToken) : IRequest<ApiResponse<AuthTokensDto>>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler(
    IJwtTokenService jwtService,
    IApplicationDbContext db,
    ILogger<RefreshTokenCommandHandler> logger) : IRequestHandler<RefreshTokenCommand, ApiResponse<AuthTokensDto>>
{
    public async Task<ApiResponse<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var userId = await jwtService.ValidateRefreshTokenAsync(request.RefreshToken, ct);
        if (userId is null)
            return ApiResponse<AuthTokensDto>.Fail("REFRESH_TOKEN_INVALID", "Invalid or expired refresh token.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.IsBlocked)
            return ApiResponse<AuthTokensDto>.Fail("USER_NOT_FOUND", "User not found or blocked.");

        // Rotate: revoke old, issue new
        await jwtService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        var accessToken = jwtService.GenerateAccessToken(user);
        var newRefresh = await jwtService.GenerateRefreshTokenAsync(user.Id, ct);

        logger.LogInformation("Tokens rotated for user {UserId}", user.Id);
        return ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, newRefresh, false));
    }
}
