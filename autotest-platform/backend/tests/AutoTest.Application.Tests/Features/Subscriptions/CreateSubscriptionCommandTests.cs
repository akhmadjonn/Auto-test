using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Subscriptions;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Features.Subscriptions;

public class CreateSubscriptionCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };
    private readonly FakeDistributedLockService _lockService = new();
    private readonly IPaymentProviderFactory _paymentFactory = Substitute.For<IPaymentProviderFactory>();
    private readonly IPaymentProviderService _paymentProvider = Substitute.For<IPaymentProviderService>();

    public CreateSubscriptionCommandTests()
    {
        _paymentFactory.GetProvider(Arg.Any<PaymentProvider>()).Returns(_paymentProvider);
    }

    private CreateSubscriptionCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _paymentFactory, _lockService, _dateTime,
            Substitute.For<ILogger<CreateSubscriptionCommandHandler>>());

    private static async Task<SubscriptionPlan> SeedPlanAsync(IApplicationDbContext db)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Oylik", "Oylik", "Месячный"),
            Description = new LocalizedText("D", "D", "D"),
            PriceInTiyins = 2500000,
            DurationDays = 30,
            Features = "all",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    [Fact]
    public async Task Handle_ValidCharge_CreatesActiveSubscription()
    {
        using var db = TestDbContextFactory.Create();
        var plan = await SeedPlanAsync(db);
        var handler = CreateHandler(db);

        _paymentProvider.ChargeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentChargeResult(true, "txn-123", null, null));

        var result = await handler.Handle(
            new CreateSubscriptionCommand(plan.Id, PaymentProvider.Payme, "card-token-abc"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(SubscriptionStatus.Active);

        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.Active);

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Completed);
        txn.ProviderTransactionId.Should().Be("txn-123");
    }

    [Fact]
    public async Task Handle_InvalidPlan_ReturnsPlanNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CreateSubscriptionCommand(Guid.NewGuid(), PaymentProvider.Payme, "token"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("PLAN_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AlreadySubscribed_ReturnsAlreadySubscribed()
    {
        using var db = TestDbContextFactory.Create();
        var plan = await SeedPlanAsync(db);

        // Seed an existing active subscription
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId!.Value,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartsAt = _dateTime.UtcNow.AddDays(-10),
            ExpiresAt = _dateTime.UtcNow.AddDays(20),
            AutoRenew = true,
            CreatedAt = _dateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(
            new CreateSubscriptionCommand(plan.Id, PaymentProvider.Payme, "token"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("ALREADY_SUBSCRIBED");
    }

    [Fact]
    public async Task Handle_PaymentFails_FailedStatusOnBothEntities()
    {
        using var db = TestDbContextFactory.Create();
        var plan = await SeedPlanAsync(db);
        var handler = CreateHandler(db);

        _paymentProvider.ChargeAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentChargeResult(false, null, "CARD_DECLINED", "Card was declined"));

        var result = await handler.Handle(
            new CreateSubscriptionCommand(plan.Id, PaymentProvider.Payme, "bad-token"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("CARD_DECLINED");

        var sub = await db.Subscriptions.FirstAsync();
        sub.Status.Should().Be(SubscriptionStatus.None);

        var txn = await db.PaymentTransactions.FirstAsync();
        txn.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ReturnsUnauthorized()
    {
        using var db = TestDbContextFactory.Create();
        _currentUser.UserId = null;
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CreateSubscriptionCommand(Guid.NewGuid(), PaymentProvider.Payme, "token"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Validator_EmptyPlanId_Fails()
    {
        var validator = new CreateSubscriptionCommandValidator();
        var result = await validator.ValidateAsync(
            new CreateSubscriptionCommand(Guid.Empty, PaymentProvider.Payme, "token"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlanId");
    }

    [Fact]
    public async Task Validator_EmptyCardToken_Fails()
    {
        var validator = new CreateSubscriptionCommandValidator();
        var result = await validator.ValidateAsync(
            new CreateSubscriptionCommand(Guid.NewGuid(), PaymentProvider.Payme, ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardToken");
    }
}
