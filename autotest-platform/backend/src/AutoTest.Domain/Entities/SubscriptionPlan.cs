using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class SubscriptionPlan : BaseAuditableEntity
{
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText Description { get; set; } = null!;
    public long PriceInTiyins { get; set; }
    public int DurationDays { get; set; }
    public string Features { get; set; } = null!;
    public bool IsActive { get; set; }
}
