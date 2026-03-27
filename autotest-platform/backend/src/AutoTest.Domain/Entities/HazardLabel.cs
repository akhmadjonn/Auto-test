using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class HazardLabel : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText Description { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public string HazardClass { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
