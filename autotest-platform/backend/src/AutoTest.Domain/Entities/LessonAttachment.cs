namespace AutoTest.Domain.Entities;

public class LessonAttachment : BaseAuditableEntity
{
    public Guid VideoLessonId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int SortOrder { get; set; }

    public VideoLesson Lesson { get; set; } = null!;
}
