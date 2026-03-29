using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

public record RevokePremiumCommand(Guid UserId) : IRequest<ApiResponse>;

public class RevokePremiumCommandValidator : AbstractValidator<RevokePremiumCommand>
{
    public RevokePremiumCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class RevokePremiumCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    IAuditLogService auditLog,
    ICacheService cache,
    ILogger<RevokePremiumCommandHandler> logger)
    : IRequestHandler<RevokePremiumCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(RevokePremiumCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId;
        if (adminId is null)
            return ApiResponse.Fail("UNAUTHORIZED", "Not authenticated.");

        var now = dateTime.UtcNow;

        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == request.UserId
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        if (subscription is null)
            return ApiResponse.Fail("NO_ACTIVE_SUBSCRIPTION", "User has no active subscription.");

        var oldExpiresAt = subscription.ExpiresAt;
        var planName = subscription.Plan.Name.UzLatin;

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.ExpiresAt = now;
        subscription.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        await cache.RemoveAsync($"avtolider:subscription:{request.UserId}", ct);

        await auditLog.LogAsync(
            adminId.Value, AuditAction.StatusChange, "Subscription", subscription.Id.ToString(),
            new { Status = "Active", ExpiresAt = oldExpiresAt },
            new { Status = "Cancelled", ExpiresAt = now },
            null, ct);

        logger.LogInformation(
            "Revoked premium for user {UserId} — Plan: {PlanName}", request.UserId, planName);

        return ApiResponse.Ok();
    }
}
