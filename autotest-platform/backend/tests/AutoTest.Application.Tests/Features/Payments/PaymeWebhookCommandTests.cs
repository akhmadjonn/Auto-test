using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Payments;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Features.Payments;

public class PaymeWebhookCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };

    private PaymeWebhookCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _dateTime, Substitute.For<ILogger<PaymeWebhookCommandHandler>>());

    private async Task<(SubscriptionPlan Plan, Subscription Sub)> SeedSubscriptionAsync(IApplicationDbContext db)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("P", "P", "P"),
            Description = new LocalizedText("D", "D", "D"),
            PriceInTiyins = 2500000,
            DurationDays = 30,
            Features = "all",
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.SubscriptionPlans.Add(plan);

        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PlanId = plan.Id,
            Status = SubscriptionStatus.None,
            StartsAt = _dateTime.UtcNow,
            ExpiresAt = _dateTime.UtcNow.AddDays(30),
            AutoRenew = false,
            CreatedAt = _dateTime.UtcNow
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
        return (plan, sub);
    }

    private static JsonElement BuildParams(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    [Fact]
    public async Task CheckPerformTransaction_ValidSubscription_ReturnsAllow()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var @params = BuildParams(new
        {
            amount = plan.PriceInTiyins,
            account = new { subscription_id = sub.Id.ToString() }
        });

        var result = await handler.Handle(
            new PaymeWebhookCommand(1, "CheckPerformTransaction", @params), CancellationToken.None);

        result.Error.Should().BeNull();
        result.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckPerformTransaction_InvalidSubscription_ReturnsError()
    {
        using var db = TestDbContextFactory.Create();
        var handler = CreateHandler(db);

        var @params = BuildParams(new
        {
            amount = 100L,
            account = new { subscription_id = Guid.NewGuid().ToString() }
        });

        var result = await handler.Handle(
            new PaymeWebhookCommand(1, "CheckPerformTransaction", @params), CancellationToken.None);

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(-31050);
    }

    [Fact]
    public async Task CreateTransaction_CreatesPendingPaymentTransaction()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var paymeTxnId = "payme-txn-" + Guid.NewGuid().ToString("N")[..8];
        var @params = BuildParams(new
        {
            id = paymeTxnId,
            amount = plan.PriceInTiyins,
            account = new { subscription_id = sub.Id.ToString() }
        });

        var result = await handler.Handle(
            new PaymeWebhookCommand(1, "CreateTransaction", @params), CancellationToken.None);

        result.Error.Should().BeNull();

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.ProviderTransactionId.Should().Be(paymeTxnId);
        txn.Status.Should().Be(PaymentStatus.Pending);
        txn.AmountInTiyins.Should().Be(plan.PriceInTiyins);
    }

    [Fact]
    public async Task CreateTransaction_Idempotent_OnDuplicatePaymeId()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var paymeTxnId = "payme-dup-" + Guid.NewGuid().ToString("N")[..8];
        var @params = BuildParams(new
        {
            id = paymeTxnId,
            amount = plan.PriceInTiyins,
            account = new { subscription_id = sub.Id.ToString() }
        });

        // First call
        await handler.Handle(new PaymeWebhookCommand(1, "CreateTransaction", @params), CancellationToken.None);
        // Second call — same Payme ID
        var result = await handler.Handle(new PaymeWebhookCommand(2, "CreateTransaction", @params), CancellationToken.None);

        result.Error.Should().BeNull();
        // Should still be exactly 1 transaction
        var count = await db.PaymentTransactions.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task PerformTransaction_ActivatesSubscriptionAndCompletesTransaction()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var paymeTxnId = "payme-perform-" + Guid.NewGuid().ToString("N")[..8];

        // Create transaction first
        var createParams = BuildParams(new
        {
            id = paymeTxnId,
            amount = plan.PriceInTiyins,
            account = new { subscription_id = sub.Id.ToString() }
        });
        await handler.Handle(new PaymeWebhookCommand(1, "CreateTransaction", createParams), CancellationToken.None);

        // Perform transaction
        var performParams = BuildParams(new { id = paymeTxnId });
        var result = await handler.Handle(new PaymeWebhookCommand(2, "PerformTransaction", performParams), CancellationToken.None);

        result.Error.Should().BeNull();

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Completed);
        txn.CompletedAt.Should().NotBeNull();

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task CancelTransaction_MarksTransactionFailed()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var paymeTxnId = "payme-cancel-" + Guid.NewGuid().ToString("N")[..8];

        // Create transaction first
        var createParams = BuildParams(new
        {
            id = paymeTxnId,
            amount = plan.PriceInTiyins,
            account = new { subscription_id = sub.Id.ToString() }
        });
        await handler.Handle(new PaymeWebhookCommand(1, "CreateTransaction", createParams), CancellationToken.None);

        // Cancel transaction
        var cancelParams = BuildParams(new { id = paymeTxnId, reason = 1 });
        var result = await handler.Handle(new PaymeWebhookCommand(2, "CancelTransaction", cancelParams), CancellationToken.None);

        result.Error.Should().BeNull();

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Failed);
    }
}
