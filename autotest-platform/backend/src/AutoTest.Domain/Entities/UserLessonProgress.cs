namespace AutoTest.Domain.Entities;

public class UserLessonProgress : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid VideoLessonId { get; set; }
    public int WatchedSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastWatchedAt { get; set; }

    public User User { get; set; } = null!;
    public VideoLesson Lesson { get; set; } = null!;
}
