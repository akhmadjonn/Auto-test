namespace AutoTest.Domain.Entities;

public class SessionQuestion : BaseAuditableEntity
{
    public Guid ExamSessionId { get; set; }
    public Guid QuestionId { get; set; }
    public int Order { get; set; }
    public Guid? SelectedAnswerId { get; set; }
    public bool? IsCorrect { get; set; }
    public int? TimeSpentSeconds { get; set; }

    public ExamSession ExamSession { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public AnswerOption? SelectedAnswer { get; set; }
}
