namespace AutoTest.Domain.Entities;

public class AnimatedTestAnswer : BaseAuditableEntity
{
    public Guid SessionId { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    public string SelectedOptionId { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string Category { get; set; } = string.Empty;
    public int TimeSpentMs { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }

    public AnimatedTestSession Session { get; set; } = null!;
}
