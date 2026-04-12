using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class RoadSign : BaseAuditableEntity
{
    public string SignCode { get; set; } = string.Empty; // "1.1", "2.3.1", "5.8.2" etc.
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid CategoryId { get; set; }
    public RoadSignCategory Category { get; set; } = null!;
}
