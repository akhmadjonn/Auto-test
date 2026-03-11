using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class UserQuestionState
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public LeitnerBox LeitnerBox { get; set; } = LeitnerBox.Box1;
    public DateTimeOffset NextReviewDate { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }

    public User User { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
