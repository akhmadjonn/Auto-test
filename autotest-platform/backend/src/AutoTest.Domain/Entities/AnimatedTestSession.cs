namespace AutoTest.Domain.Entities;

public class AnimatedTestSession : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public short TotalQuestions { get; set; }
    public short CorrectCount { get; set; }
    public short ScorePercentage { get; set; }
    public string[] QuestionIds { get; set; } = [];

    public User User { get; set; } = null!;
    public ICollection<AnimatedTestAnswer> Answers { get; set; } = [];
}
