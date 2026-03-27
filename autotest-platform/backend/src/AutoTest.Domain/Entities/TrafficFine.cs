using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class TrafficFine : BaseAuditableEntity
{
    public string ArticleNumber { get; set; } = string.Empty;
    public LocalizedText ViolationDescription { get; set; } = null!;
    public LocalizedText? AdditionalNotes { get; set; }
    public long PenaltyAmountTiyins { get; set; }
    public long? PenaltyMaxTiyins { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
