using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class RoadMarking : BaseAuditableEntity
{
    public string MarkingCode { get; set; } = string.Empty; // "1.1", "1.14.1", "2.3" etc.
    public RoadMarkingType MarkingType { get; set; } // Horizontal or Vertical
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
