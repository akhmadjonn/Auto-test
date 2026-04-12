using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class VideoLesson : BaseAuditableEntity
{
    public Guid VideoCategoryId { get; set; }
    public LocalizedText Title { get; set; } = null!;
    public LocalizedText? Description { get; set; }
    public VideoSourceType SourceType { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int DurationSeconds { get; set; }
    public int SortOrder { get; set; }
    public bool IsFree { get; set; }
    public bool IsDownloadable { get; set; }
    public bool IsActive { get; set; } = true;

    public VideoCategory Category { get; set; } = null!;
    public ICollection<LessonAttachment> Attachments { get; set; } = [];
}
