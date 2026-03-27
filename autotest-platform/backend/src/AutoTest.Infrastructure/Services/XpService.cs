using AutoTest.Application.Common.Constants;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class XpService(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<XpService> logger) : IXpService
{
    public async Task AwardXpAsync(Guid userId, int xpAmount, string reason, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        if (user is null)
            return;

        user.TotalXp += xpAmount;
        user.Level = XpRewards.LevelFromXp(user.TotalXp);

        var dailyStat = await GetOrCreateDailyStatAsync(userId, ct);
        dailyStat.XpEarned += xpAmount;

        logger.LogDebug("Awarded {Xp} XP to user {UserId} for {Reason}. Total: {TotalXp}, Level: {Level}",
            xpAmount, userId, reason, user.TotalXp, user.Level);
    }

    public async Task UpdateStreakAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        if (user is null)
            return;

        var now = dateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var lastStudy = user.LastStudyDate.HasValue
            ? DateOnly.FromDateTime(user.LastStudyDate.Value.UtcDateTime)
            : (DateOnly?)null;

        // Already studied today — no-op
        if (lastStudy == today)
            return;

        if (lastStudy == today.AddDays(-1))
            user.CurrentStreak++;
        else
            user.CurrentStreak = 1;

        if (user.CurrentStreak > user.LongestStreak)
            user.LongestStreak = user.CurrentStreak;

        user.LastStudyDate = now;

        // Award streak bonuses
        await AwardXpAsync(userId, XpRewards.DailyStreak, "daily_streak", ct);

        if (user.CurrentStreak == 7)
            await AwardXpAsync(userId, XpRewards.WeekStreak, "week_streak", ct);
        else if (user.CurrentStreak == 30)
            await AwardXpAsync(userId, XpRewards.MonthStreak, "month_streak", ct);
    }

    public async Task RecordAnswerAsync(Guid userId, bool isCorrect, int? timeSpentSeconds = null, CancellationToken ct = default)
    {
        var dailyStat = await GetOrCreateDailyStatAsync(userId, ct);
        dailyStat.QuestionsAnswered++;
        if (isCorrect)
            dailyStat.CorrectAnswers++;
        if (timeSpentSeconds is > 0)
            dailyStat.TimeSpentSeconds += timeSpentSeconds.Value;
    }

    // Check local change tracker first to avoid redundant DB queries within the same scope
    private async Task<User?> GetUserAsync(Guid userId, CancellationToken ct) =>
        db.Users.Local.FirstOrDefault(u => u.Id == userId)
        ?? await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    private async Task<UserDailyStats> GetOrCreateDailyStatAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(dateTime.UtcNow.UtcDateTime);

        var dailyStat = db.UserDailyStats.Local
            .FirstOrDefault(s => s.UserId == userId && s.StatDate == today)
            ?? await db.UserDailyStats
                .FirstOrDefaultAsync(s => s.UserId == userId && s.StatDate == today, ct);

        if (dailyStat is null)
        {
            dailyStat = new UserDailyStats
            {
                UserId = userId,
                StatDate = today
            };
            db.UserDailyStats.Add(dailyStat);
        }

        return dailyStat;
    }
}
