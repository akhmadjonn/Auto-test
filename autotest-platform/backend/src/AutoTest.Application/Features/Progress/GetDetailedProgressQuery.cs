using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Progress;

public record GetDetailedProgressQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<DetailedProgressDto>>;

public record DetailedProgressDto(
    List<CategoryAccuracyDto> CategoryAccuracy,
    TimeAnalyticsDto TimeAnalytics,
    ImprovementTrendDto Trend,
    double ReadinessScore,
    List<string> WeakCategories);

public record CategoryAccuracyDto(
    Guid CategoryId,
    string CategoryName,
    double Accuracy,
    int TotalAttempts,
    double AvgTimeSec);

public record TimeAnalyticsDto(
    double AvgTimePerQuestion,
    double FastestTime,
    double SlowestTime,
    List<TimeDistributionDto> Distribution);

public record TimeDistributionDto(string Range, int Count);

public record ImprovementTrendDto(
    double ThisWeekAccuracy,
    double LastWeekAccuracy,
    double ChangePercent);

public class GetDetailedProgressQueryValidator : AbstractValidator<GetDetailedProgressQuery>
{
    public GetDetailedProgressQueryValidator() { }
}

public class GetDetailedProgressQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cacheService)
    : IRequestHandler<GetDetailedProgressQuery, ApiResponse<DetailedProgressDto>>
{
    public async Task<ApiResponse<DetailedProgressDto>> Handle(
        GetDetailedProgressQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<DetailedProgressDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        // Premium check: verify user has active subscription
        var hasSubscription = await db.Subscriptions
            .AsNoTracking()
            .AnyAsync(s => s.UserId == userId
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > dateTime.UtcNow, ct);

        if (!hasSubscription)
            return ApiResponse<DetailedProgressDto>.Fail("PREMIUM_REQUIRED",
                "Detailed progress analytics require an active subscription.");

        var cacheKey = $"avtolider:progress:detailed:{userId}";
        var cached = await cacheService.GetAsync<DetailedProgressDto>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<DetailedProgressDto>.Ok(cached);

        var now = dateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sevenDaysAgo = now.AddDays(-7);
        var fourteenDaysAgo = now.AddDays(-14);

        // Category accuracy from UserCategoryStats
        var catStats = await db.UserCategoryStats
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.TotalAttempts > 0)
            .Join(db.Categories.AsNoTracking(), s => s.CategoryId, c => c.Id,
                (s, c) => new { Stat = s, Category = c })
            .ToListAsync(ct);

        var categoryAccuracy = catStats.Select(x =>
        {
            var accuracy = x.Stat.TotalAttempts > 0
                ? Math.Round((double)x.Stat.CorrectAttempts / x.Stat.TotalAttempts * 100, 1)
                : 0.0;
            return new CategoryAccuracyDto(
                x.Category.Id,
                x.Category.Name.Get(request.Language),
                accuracy,
                x.Stat.TotalAttempts,
                0);
        }).ToList();

        // Time analytics from SessionQuestions (last 30 days)
        var timeData = await db.SessionQuestions
            .AsNoTracking()
            .Where(sq => sq.ExamSession.UserId == userId
                && sq.ExamSession.Status == ExamStatus.Completed
                && sq.ExamSession.CompletedAt >= thirtyDaysAgo
                && sq.TimeSpentSeconds.HasValue && sq.TimeSpentSeconds > 0)
            .Select(sq => sq.TimeSpentSeconds!.Value)
            .ToListAsync(ct);

        var timeAnalytics = CalculateTimeAnalytics(timeData);

        // Improvement trend: this week vs last week
        var thisWeekQuestions = await db.SessionQuestions
            .AsNoTracking()
            .Where(sq => sq.ExamSession.UserId == userId
                && sq.ExamSession.Status == ExamStatus.Completed
                && sq.ExamSession.CompletedAt >= sevenDaysAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Correct = g.Count(sq => sq.IsCorrect == true)
            })
            .FirstOrDefaultAsync(ct);

        var lastWeekQuestions = await db.SessionQuestions
            .AsNoTracking()
            .Where(sq => sq.ExamSession.UserId == userId
                && sq.ExamSession.Status == ExamStatus.Completed
                && sq.ExamSession.CompletedAt >= fourteenDaysAgo
                && sq.ExamSession.CompletedAt < sevenDaysAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Correct = g.Count(sq => sq.IsCorrect == true)
            })
            .FirstOrDefaultAsync(ct);

        var thisWeekAcc = thisWeekQuestions is { Total: > 0 }
            ? Math.Round((double)thisWeekQuestions.Correct / thisWeekQuestions.Total * 100, 1)
            : 0.0;
        var lastWeekAcc = lastWeekQuestions is { Total: > 0 }
            ? Math.Round((double)lastWeekQuestions.Correct / lastWeekQuestions.Total * 100, 1)
            : 0.0;
        var changePct = lastWeekAcc > 0
            ? Math.Round(thisWeekAcc - lastWeekAcc, 1)
            : 0.0;

        var trend = new ImprovementTrendDto(thisWeekAcc, lastWeekAcc, changePct);

        // Readiness score: weighted average of category accuracies
        var totalQuestionCount = catStats.Sum(x => x.Stat.TotalAttempts);
        var readinessScore = totalQuestionCount > 0
            ? Math.Round(catStats.Sum(x =>
            {
                var acc = x.Stat.TotalAttempts > 0
                    ? (double)x.Stat.CorrectAttempts / x.Stat.TotalAttempts * 100
                    : 0;
                var weight = (double)x.Stat.TotalAttempts / totalQuestionCount;
                return acc * weight;
            }), 1)
            : 0.0;

        var weakCategories = categoryAccuracy
            .Where(c => c.Accuracy < 70)
            .OrderBy(c => c.Accuracy)
            .Select(c => c.CategoryName)
            .ToList();

        var result = new DetailedProgressDto(
            categoryAccuracy, timeAnalytics, trend, readinessScore, weakCategories);

        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);

        return ApiResponse<DetailedProgressDto>.Ok(result);
    }

    private static TimeAnalyticsDto CalculateTimeAnalytics(List<int> timeData)
    {
        if (timeData.Count == 0)
            return new TimeAnalyticsDto(0, 0, 0, []);

        var avg = Math.Round(timeData.Average(), 1);
        var min = (double)timeData.Min();
        var max = (double)timeData.Max();

        var distribution = new List<TimeDistributionDto>
        {
            new("0-5s", timeData.Count(t => t <= 5)),
            new("5-10s", timeData.Count(t => t > 5 && t <= 10)),
            new("10-20s", timeData.Count(t => t > 10 && t <= 20)),
            new("20-30s", timeData.Count(t => t > 20 && t <= 30)),
            new("30s+", timeData.Count(t => t > 30))
        };

        return new TimeAnalyticsDto(avg, min, max, distribution);
    }
}
