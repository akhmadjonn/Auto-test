using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class VideoCategory : BaseAuditableEntity
{
    public LocalizedText Name { get; set; } = null!;
    public LocalizedText? Description { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VideoLesson> Lessons { get; set; } = [];
}
