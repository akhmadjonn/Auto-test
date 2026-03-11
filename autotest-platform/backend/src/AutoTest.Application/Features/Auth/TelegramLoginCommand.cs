using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Auth;

public record TelegramLoginCommand(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    long AuthDate,
    string Hash) : IRequest<ApiResponse<AuthTokensDto>>;

public class TelegramLoginCommandValidator : AbstractValidator<TelegramLoginCommand>
{
    public TelegramLoginCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.Hash).NotEmpty();
        RuleFor(x => x.AuthDate).GreaterThan(0);
    }
}

public class TelegramLoginCommandHandler(
    IApplicationDbContext db,
    IJwtTokenService jwtService,
    ITelegramAuthService telegramAuth,
    IDateTimeProvider dateTime,
    ILogger<TelegramLoginCommandHandler> logger) : IRequestHandler<TelegramLoginCommand, ApiResponse<AuthTokensDto>>
{
    public async Task<ApiResponse<AuthTokensDto>> Handle(TelegramLoginCommand request, CancellationToken ct)
    {
        // Auth_date freshness check (max 5 minutes old)
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - request.AuthDate > 300)
            return ApiResponse<AuthTokensDto>.Fail("TELEGRAM_AUTH_EXPIRED", "Telegram auth data is expired.");

        // Verify HMAC-SHA256 hash from Telegram widget
        if (!telegramAuth.VerifyHash(request.Id, request.FirstName, request.LastName,
                request.Username, request.PhotoUrl, request.AuthDate, request.Hash))
            return ApiResponse<AuthTokensDto>.Fail("TELEGRAM_INVALID_HASH", "Invalid Telegram authentication hash.");

        var isNew = false;
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramId == request.Id, ct);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TelegramId = request.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = UserRole.User,
                AuthProvider = AuthProvider.Telegram,
                PreferredLanguage = Language.UzLatin,
                CreatedAt = dateTime.UtcNow,
                UpdatedAt = dateTime.UtcNow
            };
            db.Users.Add(user);
            isNew = true;
        }
        else
        {
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
        }

        user.LastActiveAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user.Id, ct);

        logger.LogInformation("Telegram user {TgId} authenticated (new: {IsNew})", request.Id, isNew);
        return ApiResponse<AuthTokensDto>.Ok(new AuthTokensDto(accessToken, refreshToken, isNew));
    }
}
