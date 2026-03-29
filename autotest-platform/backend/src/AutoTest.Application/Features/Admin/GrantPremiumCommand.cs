using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

public record GrantPremiumCommand(
    Guid UserId,
    Guid PlanId,
    int? DurationDays,
    string? Note) : IRequest<ApiResponse<GrantPremiumResultDto>>;

public record GrantPremiumResultDto(
    Guid SubscriptionId,
    DateTimeOffset ExpiresAt,
    string PlanName,
    bool IsForever);

public class GrantPremiumCommandValidator : AbstractValidator<GrantPremiumCommand>
{
    public GrantPremiumCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.DurationDays)
            .GreaterThan(0).When(x => x.DurationDays.HasValue)
            .WithMessage("Duration must be greater than 0 days.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class GrantPremiumCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    IAuditLogService auditLog,
    ICacheService cache,
    ILogger<GrantPremiumCommandHandler> logger)
    : IRequestHandler<GrantPremiumCommand, ApiResponse<GrantPremiumResultDto>>
{
    public async Task<ApiResponse<GrantPremiumResultDto>> Handle(GrantPremiumCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId;
        if (adminId is null)
            return ApiResponse<GrantPremiumResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var now = dateTime.UtcNow;

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null)
            return ApiResponse<GrantPremiumResultDto>.Fail("USER_NOT_FOUND", "User not found.");

        var plan = await db.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);
        if (plan is null)
            return ApiResponse<GrantPremiumResultDto>.Fail("PLAN_NOT_FOUND", "Subscription plan not found or inactive.");

        var isForever = request.DurationDays is null;
        var expiresAt = isForever
            ? new DateTimeOffset(2099, 12, 31, 23, 59, 59, TimeSpan.Zero)
            : now.AddDays(request.DurationDays!.Value);

        // Check existing active subscription — extend it
        var existingSub = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == request.UserId
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        Guid subscriptionId;

        if (existingSub is not null)
        {
            // Extend: add duration to current ExpiresAt, or set forever
            existingSub.ExpiresAt = isForever
                ? new DateTimeOffset(2099, 12, 31, 23, 59, 59, TimeSpan.Zero)
                : existingSub.ExpiresAt.AddDays(request.DurationDays!.Value);
            existingSub.PlanId = plan.Id;
            existingSub.PaymentProvider = PaymentProvider.Manual;
            existingSub.UpdatedAt = now;
            subscriptionId = existingSub.Id;
            expiresAt = existingSub.ExpiresAt;
        }
        else
        {
            // Create new subscription
            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                StartsAt = now,
                ExpiresAt = expiresAt,
                AutoRenew = false,
                CardToken = null,
                PaymentProvider = PaymentProvider.Manual,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Subscriptions.Add(subscription);
            subscriptionId = subscription.Id;
        }

        // Create audit payment transaction (amount = 0, completed immediately)
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            SubscriptionId = subscriptionId,
            Provider = PaymentProvider.Manual,
            AmountInTiyins = 0,
            Status = PaymentStatus.Completed,
            CompletedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.PaymentTransactions.Add(transaction);

        await db.SaveChangesAsync(ct);

        // Invalidate cached subscription state
        await cache.RemoveAsync($"avtolider:subscription:{request.UserId}", ct);

        var durationLabel = isForever ? "forever" : $"{request.DurationDays} days";
        await auditLog.LogAsync(
            adminId.Value, AuditAction.StatusChange, "Subscription", subscriptionId.ToString(),
            null,
            new { PlanName = plan.Name.UzLatin, Duration = durationLabel, Note = request.Note },
            null, ct);

        logger.LogInformation(
            "Granted premium to user {Phone} — Plan: {PlanName}, Duration: {Duration}",
            user.PhoneNumber, plan.Name.UzLatin, durationLabel);

        return ApiResponse<GrantPremiumResultDto>.Ok(new GrantPremiumResultDto(
            subscriptionId, expiresAt, plan.Name.UzLatin, isForever));
    }
}
