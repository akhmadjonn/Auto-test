using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Subscriptions;

public record GetSubscriptionStatusQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<SubscriptionStatusDto>>;

public record SubscriptionStatusDto(
    string Status,
    LocalizedText? PlanName,
    DateTimeOffset? ExpiresAt,
    bool AutoRenew,
    Guid? SubscriptionId);

public class GetSubscriptionStatusQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime)
    : IRequestHandler<GetSubscriptionStatusQuery, ApiResponse<SubscriptionStatusDto>>
{
    public async Task<ApiResponse<SubscriptionStatusDto>> Handle(
        GetSubscriptionStatusQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<SubscriptionStatusDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var subscription = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.ExpiresAt > now)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return ApiResponse<SubscriptionStatusDto>.Ok(new SubscriptionStatusDto(
                "none", null, null, false, null));

        return ApiResponse<SubscriptionStatusDto>.Ok(new SubscriptionStatusDto(
            "active",
            subscription.Plan.Name,
            subscription.ExpiresAt,
            subscription.AutoRenew,
            subscription.Id));
    }
}
