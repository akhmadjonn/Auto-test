using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class PaymentTransaction : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public PaymentProvider Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public long AmountInTiyins { get; set; }
    public string Currency { get; set; } = "UZS";
    public PaymentStatus Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public Subscription? Subscription { get; set; }
}
