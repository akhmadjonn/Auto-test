using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Progress;

public record GetCategoryPerformanceQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<List<CategoryPerformanceDto>>>;

public record CategoryPerformanceDto(
    Guid CategoryId,
    string CategoryName,
    int TotalQuestions,
    int TotalAttempts,
    int CorrectAttempts,
    double Accuracy,
    int MasteredCount,   // LeitnerBox == Box5
    int DueCount,        // NextReviewDate <= now
    int NewCount);       // No state yet

public class GetCategoryPerformanceQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime)
    : IRequestHandler<GetCategoryPerformanceQuery, ApiResponse<List<CategoryPerformanceDto>>>
{
    public async Task<ApiResponse<List<CategoryPerformanceDto>>> Handle(
        GetCategoryPerformanceQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<List<CategoryPerformanceDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

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

        // Load Leitner states grouped by category
        var questionStates = await db.UserQuestionStates
            .AsNoTracking()
            .Include(s => s.Question)
            .Where(s => s.UserId == userId && categoryIds.Contains(s.Question.CategoryId))
            .Select(s => new
            {
                s.Question.CategoryId,
                s.LeitnerBox,
                s.NextReviewDate
            })
            .ToListAsync(ct);

        // Count questions per category that user has never attempted
        var practicedQuestionCountByCategory = questionStates
            .GroupBy(s => s.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        var masteredByCategory = questionStates
            .Where(s => s.LeitnerBox == LeitnerBox.Box5)
            .GroupBy(s => s.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        var dueByCategory = questionStates
            .Where(s => s.NextReviewDate <= now)
            .GroupBy(s => s.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = categories.Select(c =>
        {
            stats.TryGetValue(c.Id, out var stat);
            masteredByCategory.TryGetValue(c.Id, out var mastered);
            dueByCategory.TryGetValue(c.Id, out var due);
            practicedQuestionCountByCategory.TryGetValue(c.Id, out var practiced);

            var total = stat?.TotalAttempts ?? 0;
            var correct = stat?.CorrectAttempts ?? 0;
            var accuracy = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0.0;
            var newCount = c.TotalQuestions - practiced;

            return new CategoryPerformanceDto(
                c.Id,
                c.Name.Get(request.Language),
                c.TotalQuestions,
                total,
                correct,
                accuracy,
                mastered,
                due,
                Math.Max(0, newCount));
        })
        .OrderByDescending(c => c.TotalAttempts)
        .ThenBy(c => c.Accuracy)
        .ToList();

        return ApiResponse<List<CategoryPerformanceDto>>.Ok(result);
    }
}
