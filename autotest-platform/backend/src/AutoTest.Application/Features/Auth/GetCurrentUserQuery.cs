using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Auth;

public record GetCurrentUserQuery : IRequest<ApiResponse<CurrentUserDto>>;

public record CurrentUserDto(
    Guid Id,
    string? PhoneNumber,
    string? FirstName,
    string? LastName,
    UserRole Role,
    Language PreferredLanguage,
    bool HasActiveSubscription);

public class GetCurrentUserQueryHandler(
    ICurrentUser currentUser,
    IApplicationDbContext db) : IRequestHandler<GetCurrentUserQuery, ApiResponse<CurrentUserDto>>
{
    public async Task<ApiResponse<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<CurrentUserDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

        if (user is null)
            return ApiResponse<CurrentUserDto>.Fail("USER_NOT_FOUND", "User not found.");

        var now = DateTimeOffset.UtcNow;
        var hasSubscription = await db.Subscriptions
            .AnyAsync(s => s.UserId == user.Id
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        return ApiResponse<CurrentUserDto>.Ok(new CurrentUserDto(
            user.Id,
            user.PhoneNumber,
            user.FirstName,
            user.LastName,
            user.Role,
            user.PreferredLanguage,
            hasSubscription));
    }
}
