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

public class CancelSubscriptionCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };

    private CancelSubscriptionCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _dateTime,
            Substitute.For<ILogger<CancelSubscriptionCommandHandler>>());

    private async Task<(SubscriptionPlan Plan, Subscription Sub)> SeedActiveSubscriptionAsync(IApplicationDbContext db)
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
            UserId = _currentUser.UserId!.Value,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartsAt = _dateTime.UtcNow.AddDays(-5),
            ExpiresAt = _dateTime.UtcNow.AddDays(25),
            AutoRenew = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();
        return (plan, sub);
    }

    [Fact]
    public async Task Handle_CancelOwn_DisablesAutoRenewKeepsAccess()
    {
        using var db = TestDbContextFactory.Create();
        var (_, sub) = await SeedActiveSubscriptionAsync(db);
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CancelSubscriptionCommand(sub.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.AutoRenewDisabled.Should().BeTrue();
        result.Data.ExpiresAt.Should().Be(sub.ExpiresAt);

        var updated = await db.Subscriptions.FindAsync(sub.Id);
        updated!.AutoRenew.Should().BeFalse();
        updated.Status.Should().Be(SubscriptionStatus.Active); // still active until expiry
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsSubscriptionNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CancelSubscriptionCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("SUBSCRIPTION_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsSubscriptionNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var (_, sub) = await SeedActiveSubscriptionAsync(db);

        // Switch to a different user
        _currentUser.UserId = Guid.NewGuid();
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CancelSubscriptionCommand(sub.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("SUBSCRIPTION_NOT_FOUND");
    }
}
