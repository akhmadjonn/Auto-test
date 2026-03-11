using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Progress;

public record GetUserDashboardQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<UserDashboardDto>>;

public record UserDashboardDto(
    int TotalPracticed,
    int ExamsTaken,
    double AvgScore,
    int CurrentStreak,
    int DueForReview,
    int PracticedToday,
    double PassRate,
    List<RecentExamDto> RecentExams,
    List<DailyAccuracyDto> AccuracyLast30Days);

public record RecentExamDto(
    Guid SessionId,
    string ExamTitle,
    int Score,
    bool Passed,
    int TotalQuestions,
    int CorrectAnswers,
    DateTimeOffset CompletedAt);

public record DailyAccuracyDto(DateOnly Date, double Accuracy, int QuestionCount);

public class GetUserDashboardQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime)
    : IRequestHandler<GetUserDashboardQuery, ApiResponse<UserDashboardDto>>
{
    public async Task<ApiResponse<UserDashboardDto>> Handle(GetUserDashboardQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<UserDashboardDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;
        var todayStart = now.Date;
        var thirtyDaysAgo = now.AddDays(-30);

        // Fire independent queries in parallel
        var totalPracticedTask = db.UserQuestionStates
            .CountAsync(s => s.UserId == userId, ct);

        var completedExamsTask = db.ExamSessions
            .AsNoTracking()
            .Include(s => s.ExamTemplate)
            .Where(s => s.UserId == userId
                && s.Status == ExamStatus.Completed
                && s.Mode == ExamMode.Exam)
            .OrderByDescending(s => s.CompletedAt)
            .ToListAsync(ct);

        var dueForReviewTask = db.UserQuestionStates
            .CountAsync(s => s.UserId == userId && s.NextReviewDate <= now, ct);

        var practicedTodayTask = db.UserQuestionStates
            .CountAsync(s => s.UserId == userId && s.LastAttemptAt >= todayStart, ct);

        // Daily accuracy from exam session questions (last 30 days)
        var dailyAccuracyRawTask = db.SessionQuestions
            .AsNoTracking()
            .Where(sq => sq.ExamSession.UserId == userId
                && sq.ExamSession.Status == ExamStatus.Completed
                && sq.ExamSession.CompletedAt >= thirtyDaysAgo)
            .Select(sq => new
            {
                Date = sq.ExamSession.CompletedAt!.Value.Date,
                IsCorrect = sq.IsCorrect == true
            })
            .ToListAsync(ct);

        // Streak needs exam + practice dates
        var examDatesTask = db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == ExamStatus.Completed && s.CompletedAt.HasValue)
            .Select(s => s.CompletedAt!.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        var practiceDatesTask = db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.LastAttemptAt.HasValue)
            .Select(s => s.LastAttemptAt!.Value.Date)
            .Distinct()
            .ToListAsync(ct);

        await Task.WhenAll(
            totalPracticedTask, completedExamsTask, dueForReviewTask,
            practicedTodayTask, dailyAccuracyRawTask, examDatesTask, practiceDatesTask);

        var completedExams = await completedExamsTask;
        var examsTaken = completedExams.Count;
        var avgScore = examsTaken > 0 ? completedExams.Average(e => e.Score ?? 0) : 0.0;
        var passRate = examsTaken > 0
            ? (double)completedExams.Count(e => (e.Score ?? 0) >= (e.ExamTemplate?.PassingScore ?? 80))
              / examsTaken * 100.0
            : 0.0;

        var streak = CalculateStreak(await examDatesTask, await practiceDatesTask);

        var dailyAccuracy = (await dailyAccuracyRawTask)
            .GroupBy(x => x.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyAccuracyDto(
                DateOnly.FromDateTime(g.Key),
                g.Count() > 0 ? Math.Round((double)g.Count(x => x.IsCorrect) / g.Count() * 100, 1) : 0,
                g.Count()))
            .ToList();

        var recentExams = completedExams
            .Take(10)
            .Select(s => new RecentExamDto(
                s.Id,
                s.ExamTemplate?.Title.Get(request.Language) ?? "Exam",
                s.Score ?? 0,
                (s.Score ?? 0) >= (s.ExamTemplate?.PassingScore ?? 80),
                s.ExamTemplate?.TotalQuestions ?? 0,
                s.CorrectAnswers ?? 0,
                s.CompletedAt ?? s.UpdatedAt ?? DateTimeOffset.UtcNow))
            .ToList();

        return ApiResponse<UserDashboardDto>.Ok(new UserDashboardDto(
            await totalPracticedTask,
            examsTaken,
            Math.Round(avgScore, 1),
            streak,
            await dueForReviewTask,
            await practicedTodayTask,
            Math.Round(passRate, 1),
            recentExams,
            dailyAccuracy));
    }

    private static int CalculateStreak(List<DateTime> examDates, List<DateTime> practiceDates)
    {
        var activeDates = new HashSet<DateOnly>(
            examDates.Select(DateOnly.FromDateTime)
                .Concat(practiceDates.Select(DateOnly.FromDateTime)));

        if (activeDates.Count == 0)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var streak = 0;
        var current = today;

        // Check today first, then go backward
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
