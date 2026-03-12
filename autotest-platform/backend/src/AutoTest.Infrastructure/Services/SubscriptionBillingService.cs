using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class SubscriptionBillingService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionBillingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger startup to avoid thundering herd
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunBillingCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Billing cycle failed");
            }

            // Run once per day
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunBillingCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var paymentFactory = scope.ServiceProvider.GetRequiredService<IPaymentProviderFactory>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTime.UtcNow;
        // Process subscriptions expiring within the next 24 hours
        var renewalWindow = now.AddHours(24);

        var subscriptionsToRenew = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active
                && s.AutoRenew
                && s.ExpiresAt <= renewalWindow
                && s.ExpiresAt > now
                && s.CardToken != null
                && s.PaymentProvider != null)
            .ToListAsync(ct);

        logger.LogInformation("Billing cycle: found {Count} subscriptions to renew", subscriptionsToRenew.Count);

        foreach (var subscription in subscriptionsToRenew)
        {
            await ProcessRenewalAsync(subscription, db, paymentFactory, now, ct);
        }

        // Expire overdue active subscriptions that weren't renewed
        var expiredCount = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SubscriptionStatus.Expired)
                .SetProperty(x => x.UpdatedAt, now), ct);

        if (expiredCount > 0)
            logger.LogInformation("Expired {Count} subscriptions", expiredCount);
    }

    private async Task ProcessRenewalAsync(
        Subscription subscription,
        IApplicationDbContext db,
        IPaymentProviderFactory paymentFactory,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            var provider = paymentFactory.GetProvider(subscription.PaymentProvider!.Value);

            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                UserId = subscription.UserId,
                SubscriptionId = subscription.Id,
                Provider = subscription.PaymentProvider!.Value,
                AmountInTiyins = subscription.Plan.PriceInTiyins,
                Status = PaymentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.PaymentTransactions.Add(transaction);
            await db.SaveChangesAsync(ct);

            var chargeResult = await provider.ChargeAsync(
                subscription.CardToken!,
                subscription.Plan.PriceInTiyins,
                subscription.Id,
                $"Avtolider renewal: {subscription.Plan.Name.UzLatin}",
                ct);

            if (chargeResult.Success)
            {
                subscription.ExpiresAt = subscription.ExpiresAt.AddDays(subscription.Plan.DurationDays);
                subscription.UpdatedAt = now;
                transaction.Status = PaymentStatus.Completed;
                transaction.ProviderTransactionId = chargeResult.TransactionId;
                transaction.CompletedAt = now;

                logger.LogInformation(
                    "Subscription renewed: sub={SubscriptionId} user={UserId} newExpiry={Expiry}",
                    subscription.Id, subscription.UserId, subscription.ExpiresAt);
            }
            else
            {
                // Failed renewal — suspend (user can reactivate manually)
                subscription.Status = SubscriptionStatus.Expired;
                subscription.UpdatedAt = now;
                transaction.Status = PaymentStatus.Failed;

                logger.LogWarning(
                    "Renewal failed: sub={SubscriptionId} user={UserId} error={Error}",
                    subscription.Id, subscription.UserId, chargeResult.ErrorMessage);
            }

            transaction.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Renewal processing exception: sub={SubscriptionId}", subscription.Id);
        }
    }
}
