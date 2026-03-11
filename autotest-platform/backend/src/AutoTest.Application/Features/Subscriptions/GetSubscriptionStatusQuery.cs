using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Subscriptions;

public record GetSubscriptionStatusQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<SubscriptionStatusDto>>;

public record SubscriptionStatusDto(
    bool IsActive,
    Guid? SubscriptionId,
    string? PlanName,
    long? PriceInTiyins,
    DateTimeOffset? StartsAt,
    DateTimeOffset? ExpiresAt,
    bool AutoRenew,
    PaymentProvider? PaymentProvider,
    int? DaysRemaining);

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
                false, null, null, null, null, null, false, null, null));

        var daysRemaining = (int)Math.Ceiling((subscription.ExpiresAt - now).TotalDays);

        return ApiResponse<SubscriptionStatusDto>.Ok(new SubscriptionStatusDto(
            true,
            subscription.Id,
            subscription.Plan.Name.Get(request.Language),
            subscription.Plan.PriceInTiyins,
            subscription.StartsAt,
            subscription.ExpiresAt,
            subscription.AutoRenew,
            subscription.PaymentProvider,
            daysRemaining));
    }
}
