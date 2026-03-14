using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Progress;

public record GetCategoryPerformanceQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<List<CategoryPerformanceDto>>>;

public record CategoryPerformanceDto(
    Guid CategoryId,
    LocalizedText CategoryName,
    int TotalAttempts,
    int CorrectAttempts,
    double Accuracy,
    int QuestionsInCategory,
    int QuestionsPracticed);

public class GetCategoryPerformanceQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ICacheService cacheService)
    : IRequestHandler<GetCategoryPerformanceQuery, ApiResponse<List<CategoryPerformanceDto>>>
{
    public async Task<ApiResponse<List<CategoryPerformanceDto>>> Handle(
        GetCategoryPerformanceQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<List<CategoryPerformanceDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        // Redis cache — 60s TTL
        var cacheKey = $"avtolider:catperf:{userId}";
        var cached = await cacheService.GetAsync<List<CategoryPerformanceDto>>(cacheKey, ct);
        if (cached is not null)
            return ApiResponse<List<CategoryPerformanceDto>>.Ok(cached);

        // Load categories that have active questions
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.Questions.Any(q => q.IsActive))
            .Select(c => new
            {
                c.Id,
                Name = c.Name,
                TotalQuestions = c.Questions.Count(q => q.IsActive)
            })
            .ToListAsync(ct);

        var categoryIds = categories.Select(c => c.Id).ToList();

        // Load user stats per category
        var stats = await db.UserCategoryStats
            .AsNoTracking()
            .Where(s => s.UserId == userId && categoryIds.Contains(s.CategoryId))
            .ToDictionaryAsync(s => s.CategoryId, ct);

        // Count practiced questions per category
        var practicedQuestionCountByCategory = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && categoryIds.Contains(s.Question.CategoryId))
            .GroupBy(s => s.Question.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, ct);

        var result = categories.Select(c =>
        {
            stats.TryGetValue(c.Id, out var stat);
            practicedQuestionCountByCategory.TryGetValue(c.Id, out var practiced);

            var total = stat?.TotalAttempts ?? 0;
            var correct = stat?.CorrectAttempts ?? 0;
            var accuracy = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0.0;

            return new CategoryPerformanceDto(
                c.Id,
                c.Name,
                total,
                correct,
                accuracy,
                c.TotalQuestions,
                practiced);
        })
        .OrderByDescending(c => c.TotalAttempts)
        .ThenBy(c => c.Accuracy)
        .ToList();

        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(60), ct);

        return ApiResponse<List<CategoryPerformanceDto>>.Ok(result);
    }
}
