using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Payments;

public record InitiatePaymentCommand(
    Guid PlanId,
    PaymentProvider Provider) : IRequest<ApiResponse<InitiatePaymentResultDto>>;

public record InitiatePaymentResultDto(
    Guid SubscriptionId,
    Guid TransactionId,
    string? ProviderTransactionId);

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}

public class InitiatePaymentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPaymentProviderFactory paymentFactory,
    IDistributedLockService lockService,
    IDateTimeProvider dateTime,
    ILogger<InitiatePaymentCommandHandler> logger)
    : IRequestHandler<InitiatePaymentCommand, ApiResponse<InitiatePaymentResultDto>>
{
    public async Task<ApiResponse<InitiatePaymentResultDto>> Handle(
        InitiatePaymentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<InitiatePaymentResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        await using var lockHandle = await lockService.TryAcquireAsync(
            $"avtolider:lock:subscription:{userId}", TimeSpan.FromSeconds(30), ct);
        if (lockHandle is null)
            return ApiResponse<InitiatePaymentResultDto>.Fail("CONCURRENT_REQUEST", "Payment initiation in progress.");

        var plan = await db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);

        if (plan is null)
            return ApiResponse<InitiatePaymentResultDto>.Fail("PLAN_NOT_FOUND", "Subscription plan not found.");

        var hasActive = await db.Subscriptions
            .AnyAsync(s => s.UserId == userId
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        if (hasActive)
            return ApiResponse<InitiatePaymentResultDto>.Fail(
                "ALREADY_SUBSCRIBED", "You already have an active subscription.");

        var subscriptionId = Guid.NewGuid();
        var subscription = new Subscription
        {
            Id = subscriptionId,
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.None,
            StartsAt = now,
            ExpiresAt = now.AddDays(plan.DurationDays),
            AutoRenew = false,
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

        // Create payment on provider side — returns provider transaction ID
        var provider = paymentFactory.GetProvider(request.Provider);
        var providerTxnId = await provider.CreatePaymentAsync(subscriptionId, plan.PriceInTiyins, ct);

        transaction.ProviderTransactionId = providerTxnId;
        transaction.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Payment initiated: user={UserId} plan={PlanId} sub={SubscriptionId} provider={Provider}",
            userId, request.PlanId, subscriptionId, request.Provider);

        return ApiResponse<InitiatePaymentResultDto>.Ok(new InitiatePaymentResultDto(
            subscriptionId, transaction.Id, providerTxnId));
    }
}
