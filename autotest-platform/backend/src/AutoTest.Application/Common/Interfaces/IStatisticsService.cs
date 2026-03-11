namespace AutoTest.Application.Common.Interfaces;

public interface IStatisticsService
{
    Task<UserStats> GetUserStatsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryAccuracy>> GetCategoryAccuracyAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DailyActivity>> GetDailyActivityAsync(Guid userId, int days = 30, CancellationToken ct = default);
    Task<int> GetCurrentStreakAsync(Guid userId, CancellationToken ct = default);
}

public record UserStats(
    int TotalQuestionsSolved,
    int TotalCorrect,
    int TotalExamsTaken,
    int ExamsPassed,
    int CurrentStreak,
    int LongestStreak,
    double OverallAccuracy);

public record CategoryAccuracy(Guid CategoryId, string CategoryName, int Total, int Correct, double Accuracy);

public record DailyActivity(DateOnly Date, int QuestionsSolved, int Correct);
