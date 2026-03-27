namespace AutoTest.Domain.Entities;

public class UserDailyStats
{
    public Guid UserId { get; set; }
    public DateOnly StatDate { get; set; }
    public int QuestionsAnswered { get; set; }
    public int CorrectAnswers { get; set; }
    public long XpEarned { get; set; }
    public int ExamsCompleted { get; set; }
    public int TimeSpentSeconds { get; set; }

    public User User { get; set; } = null!;
}
