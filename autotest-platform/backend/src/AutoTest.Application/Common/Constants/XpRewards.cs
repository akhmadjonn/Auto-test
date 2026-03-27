namespace AutoTest.Application.Common.Constants;

public static class XpRewards
{
    public const int CorrectAnswer = 10;
    public const int IncorrectAnswer = 2;
    public const int ExamCompleted = 50;
    public const int ExamPassed = 100;
    public const int PerfectExam = 200;
    public const int DailyStreak = 25;
    public const int WeekStreak = 100;
    public const int MonthStreak = 500;
    public const int HardModeCorrect = 20;
    public const int SpeedChallengeCorrect = 15;
    public const int ReviewCorrect = 15;

    public static int LevelFromXp(long totalXp) =>
        totalXp switch
        {
            < 500 => 1,
            < 1500 => 2,
            < 3000 => 3,
            < 5000 => 4,
            < 8000 => 5,
            < 12000 => 6,
            < 18000 => 7,
            < 25000 => 8,
            < 35000 => 9,
            _ => 10
        };
}
