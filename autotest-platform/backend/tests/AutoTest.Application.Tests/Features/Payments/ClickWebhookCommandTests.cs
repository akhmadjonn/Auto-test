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

public class ClickWebhookCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };

    private ClickWebhookCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _dateTime, Substitute.For<ILogger<ClickWebhookCommandHandler>>());

    private async Task<(SubscriptionPlan Plan, Subscription Sub)> SeedSubscriptionAsync(IApplicationDbContext db)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("P", "P", "P"),
            Description = new LocalizedText("D", "D", "D"),
            PriceInTiyins = 2500000, // 25,000 UZS
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

    private static ClickWebhookCommand MakePrepareCommand(Guid subscriptionId, decimal amount, bool signatureVerified = true) =>
        new(ClickTransId: 100001, ServiceId: 1, ClickPaydocId: 200001,
            MerchantTransId: subscriptionId.ToString(), MerchantPrepareId: null,
            Amount: amount, Action: 0, Error: 0, ErrorNote: null,
            SignTime: "2026-03-12 10:00:00", SignString: "valid-sig",
            SignatureVerified: signatureVerified);

    private static ClickWebhookCommand MakeCompleteCommand(
        Guid subscriptionId, string prepareId, decimal amount, bool signatureVerified = true, int error = 0) =>
        new(ClickTransId: 100001, ServiceId: 1, ClickPaydocId: 200001,
            MerchantTransId: subscriptionId.ToString(), MerchantPrepareId: prepareId,
            Amount: amount, Action: 1, Error: error, ErrorNote: error != 0 ? "Payment failed" : null,
            SignTime: "2026-03-12 10:00:00", SignString: "valid-sig",
            SignatureVerified: signatureVerified);

    [Fact]
    public async Task Prepare_CreatesPendingTransaction()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var amount = plan.PriceInTiyins / 100.0m; // Click sends in UZS
        var result = await handler.Handle(MakePrepareCommand(sub.Id, amount), CancellationToken.None);

        result.Error.Should().Be(0);
        result.MerchantPrepareId.Should().NotBeNullOrEmpty();

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Pending);
        txn.Provider.Should().Be(PaymentProvider.Click);
    }

    [Fact]
    public async Task Complete_ActivatesSubscription()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var amount = plan.PriceInTiyins / 100.0m;

        // Step 1: Prepare
        var prepareResult = await handler.Handle(MakePrepareCommand(sub.Id, amount), CancellationToken.None);
        var prepareId = prepareResult.MerchantPrepareId!;

        // Step 2: Complete
        var completeResult = await handler.Handle(
            MakeCompleteCommand(sub.Id, prepareId, amount), CancellationToken.None);

        completeResult.Error.Should().Be(0);

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Completed);
        txn.CompletedAt.Should().NotBeNull();

        var updatedSub = await db.Subscriptions.FindAsync(sub.Id);
        updatedSub!.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Prepare_AmountMismatch_ReturnsError()
    {
        using var db = TestDbContextFactory.Create();
        var (_, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        // Send wrong amount
        var result = await handler.Handle(MakePrepareCommand(sub.Id, 999.99m), CancellationToken.None);

        result.Error.Should().Be(-2); // InvalidAmount
    }

    [Fact]
    public async Task Complete_DuplicateComplete_IsIdempotent()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var amount = plan.PriceInTiyins / 100.0m;
        var prepareResult = await handler.Handle(MakePrepareCommand(sub.Id, amount), CancellationToken.None);
        var prepareId = prepareResult.MerchantPrepareId!;

        // First complete
        await handler.Handle(MakeCompleteCommand(sub.Id, prepareId, amount), CancellationToken.None);

        // Second complete — should be idempotent
        var result = await handler.Handle(
            MakeCompleteCommand(sub.Id, prepareId, amount), CancellationToken.None);

        result.Error.Should().Be(0);
        result.MerchantConfirmId.Should().NotBeNullOrEmpty();

        var txnCount = await db.PaymentTransactions.CountAsync();
        txnCount.Should().Be(1);
    }

    [Fact]
    public async Task Prepare_InvalidSignature_ReturnsSignError()
    {
        using var db = TestDbContextFactory.Create();
        var (plan, sub) = await SeedSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var amount = plan.PriceInTiyins / 100.0m;
        var result = await handler.Handle(
            MakePrepareCommand(sub.Id, amount, signatureVerified: false), CancellationToken.None);

        result.Error.Should().Be(-1); // SignFailed
    }
}
