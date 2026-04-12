using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class RoadSignCategory : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // "1" for warning, "2" for priority, etc.
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText Description { get; set; } = null!;
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoadSign> Signs { get; set; } = [];
}
