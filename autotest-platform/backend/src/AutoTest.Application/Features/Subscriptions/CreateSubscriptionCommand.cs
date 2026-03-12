using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Subscriptions;

public record CreateSubscriptionCommand(
    Guid PlanId,
    PaymentProvider Provider,
    string CardToken,
    bool AutoRenew = true) : IRequest<ApiResponse<CreateSubscriptionResultDto>>;

public record CreateSubscriptionResultDto(
    Guid SubscriptionId,
    SubscriptionStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string? PaymentTransactionId);

public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.CardToken).NotEmpty().MaximumLength(500);
    }
}

public class CreateSubscriptionCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPaymentProviderFactory paymentFactory,
    IDistributedLockService lockService,
    IDateTimeProvider dateTime,
    ILogger<CreateSubscriptionCommandHandler> logger)
    : IRequestHandler<CreateSubscriptionCommand, ApiResponse<CreateSubscriptionResultDto>>
{
    public async Task<ApiResponse<CreateSubscriptionResultDto>> Handle(
        CreateSubscriptionCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<CreateSubscriptionResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        // Distributed lock prevents double subscription creation for same user
        await using var lockHandle = await lockService.TryAcquireAsync(
            $"avtolider:lock:subscription:{userId}", TimeSpan.FromSeconds(30), ct);
        if (lockHandle is null)
            return ApiResponse<CreateSubscriptionResultDto>.Fail("CONCURRENT_REQUEST", "Subscription creation in progress.");

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);

        if (plan is null)
            return ApiResponse<CreateSubscriptionResultDto>.Fail("PLAN_NOT_FOUND", "Subscription plan not found.");

        // Check for existing active subscription
        var hasActive = await db.Subscriptions
            .AnyAsync(s => s.UserId == userId
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        if (hasActive)
            return ApiResponse<CreateSubscriptionResultDto>.Fail(
                "ALREADY_SUBSCRIBED", "You already have an active subscription.");

        var subscriptionId = Guid.NewGuid();
        var expiresAt = now.AddDays(plan.DurationDays);

        // Create pending subscription and transaction records
        var subscription = new Subscription
        {
            Id = subscriptionId,
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.None,
            StartsAt = now,
            ExpiresAt = expiresAt,
            AutoRenew = request.AutoRenew,
            CardToken = request.CardToken,
            PaymentProvider = request.Provider,
            CreatedAt = now,
            UpdatedAt = now
        };

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionId = subscriptionId,
            Provider = request.Provider,
            AmountInTiyins = plan.PriceInTiyins,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Subscriptions.Add(subscription);
        db.PaymentTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        // Charge the card via payment provider
        var provider = paymentFactory.GetProvider(request.Provider);
        var chargeResult = await provider.ChargeAsync(
            request.CardToken,
            plan.PriceInTiyins,
            subscriptionId,
            $"Avtolider subscription: {plan.Name.UzLatin}",
            ct);

        if (chargeResult.Success)
        {
            subscription.Status = SubscriptionStatus.Active;
            transaction.Status = PaymentStatus.Completed;
            transaction.ProviderTransactionId = chargeResult.TransactionId;
            transaction.CompletedAt = now;
        }
        else
        {
            subscription.Status = SubscriptionStatus.None;
            transaction.Status = PaymentStatus.Failed;
            logger.LogWarning(
                "Payment failed for user={UserId} plan={PlanId} error={Error}",
                userId, request.PlanId, chargeResult.ErrorMessage);
        }

        subscription.UpdatedAt = now;
        transaction.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (!chargeResult.Success)
            return ApiResponse<CreateSubscriptionResultDto>.Fail(
                chargeResult.ErrorCode ?? "PAYMENT_FAILED",
                chargeResult.ErrorMessage ?? "Payment failed. Please check your card details.");

        logger.LogInformation(
            "Subscription created: user={UserId} plan={PlanId} sub={SubscriptionId} txn={TxnId}",
            userId, request.PlanId, subscriptionId, chargeResult.TransactionId);

        return ApiResponse<CreateSubscriptionResultDto>.Ok(new CreateSubscriptionResultDto(
            subscriptionId,
            SubscriptionStatus.Active,
            subscription.StartsAt,
            subscription.ExpiresAt,
            chargeResult.TransactionId));
    }
}
