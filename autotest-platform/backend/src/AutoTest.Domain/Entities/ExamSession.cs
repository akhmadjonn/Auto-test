using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class ExamSession : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public Guid? ExamTemplateId { get; set; }
    public ExamStatus Status { get; set; }
    public ExamMode Mode { get; set; }
    public int? Score { get; set; }
    public int? CorrectAnswers { get; set; }
    public int? TimeTakenSeconds { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public LicenseCategory LicenseCategory { get; set; }
    public int? TicketNumber { get; set; }

    public User User { get; set; } = null!;
    public ExamTemplate? ExamTemplate { get; set; }
    public ICollection<SessionQuestion> SessionQuestions { get; set; } = [];
}
