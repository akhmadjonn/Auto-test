using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class Subscription : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool AutoRenew { get; set; }
    public string? CardToken { get; set; }
    public PaymentProvider? PaymentProvider { get; set; }

    public User User { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}
