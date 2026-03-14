using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Practice;

public record GetPracticeSessionQuery(
    Guid? CategoryId = null,
    Language Language = Language.UzLatin,
    int BatchSize = 10) : IRequest<ApiResponse<PracticeSessionDto>>;

public record PracticeSessionDto(
    List<PracticeQuestionDto> Questions,
    int DueReviewCount);

public record PracticeQuestionDto(
    Guid Id,
    LocalizedText Text,
    string? ImageUrl,
    LocalizedText CategoryName,
    int Difficulty,
    List<PracticeAnswerOptionDto> AnswerOptions,
    int LeitnerBox);

public record PracticeAnswerOptionDto(Guid Id, LocalizedText Text, string? ImageUrl);

public class GetPracticeSessionQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime) : IRequestHandler<GetPracticeSessionQuery, ApiResponse<PracticeSessionDto>>
{
    public async Task<ApiResponse<PracticeSessionDto>> Handle(GetPracticeSessionQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PracticeSessionDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;
        var batchSize = Math.Clamp(request.BatchSize, 5, 20);

        // Load active questions with answer options and categories
        var questionsQuery = db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Include(q => q.Category)
            .Where(q => q.IsActive);

        if (request.CategoryId.HasValue)
            questionsQuery = questionsQuery.Where(q => q.CategoryId == request.CategoryId.Value);

        var allQuestions = await questionsQuery.ToListAsync(ct);
        var questionIds = allQuestions.Select(q => q.Id).ToList();

        // Load existing Leitner states for this user
        var states = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
            .ToDictionaryAsync(s => s.QuestionId, ct);

        // Get question IDs from user's weakest categories
        var weakQuestionIds = await GetWeakCategoryQuestionIdsAsync(userId, request.CategoryId, questionIds, ct);

        // Classify questions into pools
        var duePool = allQuestions
            .Where(q => states.TryGetValue(q.Id, out var s) && s.NextReviewDate <= now)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var newPool = allQuestions
            .Where(q => !states.ContainsKey(q.Id))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        // Weak = from weak categories, not yet due (due pool already covers those)
        var weakPool = allQuestions
            .Where(q => weakQuestionIds.Contains(q.Id)
                && states.TryGetValue(q.Id, out var s)
                && s.NextReviewDate > now)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        // 60% due / 30% new / 10% weak
        var dueTarget = (int)Math.Ceiling(batchSize * 0.6);
        var newTarget = (int)Math.Ceiling(batchSize * 0.3);
        var weakTarget = Math.Max(0, batchSize - dueTarget - newTarget);

        var selected = FillBatch(duePool, newPool, weakPool, dueTarget, newTarget, weakTarget, batchSize);

        // Batch presigned URL generation — single parallel call instead of N+1
        var allImageKeys = new List<string>();
        foreach (var q in selected)
        {
            if (q.ImageUrl is not null) allImageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) allImageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(allImageKeys, ct);

        var questionDtos = selected.Select(q =>
        {
            states.TryGetValue(q.Id, out var state);
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;

            var shuffledOptions = q.AnswerOptions.OrderBy(_ => Random.Shared.Next()).ToList();
            var optDtos = shuffledOptions.Select(a =>
            {
                var optImg = a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null;
                return new PracticeAnswerOptionDto(a.Id, a.Text, optImg);
            }).ToList();

            return new PracticeQuestionDto(
                q.Id,
                q.Text,
                imgUrl,
                q.Category?.Name ?? new LocalizedText("", "", ""),
                (int)q.Difficulty,
                optDtos,
                (int)(state?.LeitnerBox ?? LeitnerBox.Box1));
        }).ToList();

        return ApiResponse<PracticeSessionDto>.Ok(new PracticeSessionDto(
            questionDtos,
            duePool.Count));
    }

    private static List<Question> FillBatch(
        List<Question> due, List<Question> newQ, List<Question> weak,
        int dueTarget, int newTarget, int weakTarget, int total)
    {
        var result = new List<Question>(total);
        var taken = new HashSet<Guid>(total);

        int TakeFrom(List<Question> pool, int count)
        {
            var taken2 = 0;
            foreach (var q in pool)
            {
                if (taken2 >= count || result.Count >= total) break;
                if (taken.Add(q.Id))
                {
                    result.Add(q);
                    taken2++;
                }
            }
            return taken2;
        }

        TakeFrom(due, dueTarget);
        TakeFrom(newQ, newTarget);
        TakeFrom(weak, weakTarget);

        // Fill remaining slots with any available questions
        if (result.Count < total)
        {
            var overflow = due.Concat(newQ).Concat(weak).ToList();
            TakeFrom(overflow, total - result.Count);
        }

        return result;
    }

    private async Task<HashSet<Guid>> GetWeakCategoryQuestionIdsAsync(
        Guid userId, Guid? categoryId, List<Guid> availableQuestionIds, CancellationToken ct)
    {
        var statsQuery = db.UserCategoryStats
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.TotalAttempts > 0);

        if (categoryId.HasValue)
            statsQuery = statsQuery.Where(s => s.CategoryId == categoryId.Value);

        // Get 3 weakest categories by accuracy
        var weakCategoryIds = await statsQuery
            .OrderBy(s => (double)s.CorrectAttempts / s.TotalAttempts)
            .Take(3)
            .Select(s => s.CategoryId)
            .ToListAsync(ct);

        if (weakCategoryIds.Count == 0)
            return [];

        var ids = await db.Questions
            .AsNoTracking()
            .Where(q => q.IsActive
                && weakCategoryIds.Contains(q.CategoryId)
                && availableQuestionIds.Contains(q.Id))
            .Select(q => q.Id)
            .ToListAsync(ct);

        return [..ids];
    }
}
