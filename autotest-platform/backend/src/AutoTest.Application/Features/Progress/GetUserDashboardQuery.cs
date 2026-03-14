using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Progress;

public record GetUserDashboardQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<UserDashboardDto>>;

public record UserDashboardDto(
    int TotalQuestionsPracticed,
    int TotalExamsTaken,
    double AverageExamScore,
    int CurrentStreak,
    int DueForReview,
    int QuestionsAnsweredToday,
    double ExamPassRate,
    List<RecentExamDto> RecentExams,
    List<DailyAccuracyDto> AccuracyOverTime);

public record RecentExamDto(
    Guid ExamId,
    int Score,
    bool Passed,
    DateTimeOffset CompletedAt);

public record DailyAccuracyDto(DateOnly Date, double Accuracy, int QuestionCount);

public class GetUserDashboardQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cacheService)
    : IRequestHandler<GetUserDashboardQuery, ApiResponse<UserDashboardDto>>
{
    public async Task<ApiResponse<UserDashboardDto>> Handle(GetUserDashboardQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<UserDashboardDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        // Redis cache — 30s TTL
        var cacheKey = $"avtolider:dashboard:{userId}";
        var cached = await cacheService.GetAsync<UserDashboardDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<UserDashboardDto>.Ok(cached);

        var now = dateTime.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var thirtyDaysAgo = now.AddDays(-30);
        var ninetyDaysAgo = now.AddDays(-90);

        // Query 1: Combine all UserQuestionStates aggregates into single query
        var uqsStats = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPracticed = g.Count(),
                DueForReview = g.Count(s => s.NextReviewDate <= now),
                PracticedToday = g.Count(s => s.LastAttemptAt >= todayStart),
            })
            .FirstOrDefaultAsync(ct);

        // Practice dates for streak (bounded to 90 days)
        var practiceDates = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.LastAttemptAt >= ninetyDaysAgo)
            .Select(s => s.LastAttemptAt!.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        // Query 2: Exam aggregates — no ToList, compute in SQL
        var examAgg = await db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ExamStatus.Completed && s.Mode == ExamMode.Exam)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                AvgScore = g.Average(s => (double)(s.Score ?? 0)),
                PassedCount = g.Count(s => (s.Score ?? 0) >= s.ExamTemplate!.PassingScore),
            })
            .FirstOrDefaultAsync(ct);

        // Exam dates for streak (bounded to 90 days)
        var examDates = await db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ExamStatus.Completed
                && s.CompletedAt.HasValue && s.CompletedAt >= ninetyDaysAgo)
            .Select(s => s.CompletedAt!.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        // Recent 10 exams — project directly, no Include
        var recentExams = await db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ExamStatus.Completed && s.Mode == ExamMode.Exam)
            .OrderByDescending(s => s.CompletedAt)
            .Take(10)
            .Select(s => new RecentExamDto(
                s.Id,
                s.Score ?? 0,
                (s.Score ?? 0) >= s.ExamTemplate!.PassingScore,
                s.CompletedAt ?? s.UpdatedAt ?? now))
            .ToListAsync(ct);

        // Query 3: Daily accuracy — GroupBy in SQL
        var dailyAccuracy = await db.SessionQuestions
            .AsNoTracking()
            .Where(sq => sq.ExamSession.UserId == userId
                && sq.ExamSession.Status == ExamStatus.Completed
                && sq.ExamSession.CompletedAt >= thirtyDaysAgo)
            .GroupBy(sq => sq.ExamSession.CompletedAt!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyAccuracyDto(
                DateOnly.FromDateTime(g.Key),
                Math.Round((double)g.Count(sq => sq.IsCorrect == true) / g.Count() * 100, 1),
                g.Count()))
            .ToListAsync(ct);

        var examsTaken = examAgg?.Count ?? 0;
        var avgScore = examAgg?.AvgScore ?? 0.0;
        var passRate = examsTaken > 0
            ? Math.Round((double)(examAgg!.PassedCount) / examsTaken * 100.0, 1)
            : 0.0;

        var streak = CalculateStreak(examDates, practiceDates, now);

        var result = new UserDashboardDto(
            uqsStats?.TotalPracticed ?? 0,
            examsTaken,
            Math.Round(avgScore, 1),
            streak,
            uqsStats?.DueForReview ?? 0,
            uqsStats?.PracticedToday ?? 0,
            passRate,
            recentExams,
            dailyAccuracy);

        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30), ct);

        return ApiResponse<UserDashboardDto>.Ok(result);
    }

    private static int CalculateStreak(List<DateTime> examDates, List<DateTime> practiceDates, DateTimeOffset now)
    {
        var activeDates = new HashSet<DateOnly>(
            examDates.Select(DateOnly.FromDateTime)
                .Concat(practiceDates.Select(DateOnly.FromDateTime)));

        if (activeDates.Count == 0)
            return 0;

        var today = DateOnly.FromDateTime(now.UtcDateTime.Date);
        var streak = 0;
        var current = today;

        while (activeDates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        // If no activity today, check if streak started yesterday
        if (streak == 0)
        {
            current = today.AddDays(-1);
            while (activeDates.Contains(current))
            {
                streak++;
                current = current.AddDays(-1);
            }
        }

        return streak;
    }
}
