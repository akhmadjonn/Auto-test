using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Practice;

public record GetQuestionsForReviewQuery(
    int Limit = 20,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<List<ReviewQuestionDto>>>;

public record ReviewQuestionDto(
    Guid Id,
    LocalizedText Text,
    string? ImageUrl,
    int LeitnerBox,
    DateTimeOffset NextReviewDate,
    List<PracticeAnswerOptionDto> AnswerOptions);

public class GetQuestionsForReviewQueryValidator : AbstractValidator<GetQuestionsForReviewQuery>
{
    public GetQuestionsForReviewQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class GetQuestionsForReviewQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime)
    : IRequestHandler<GetQuestionsForReviewQuery, ApiResponse<List<ReviewQuestionDto>>>
{
    public async Task<ApiResponse<List<ReviewQuestionDto>>> Handle(
        GetQuestionsForReviewQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<List<ReviewQuestionDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        // Get due review questions
        var dueStates = await db.UserQuestionStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.NextReviewDate <= now)
            .OrderBy(s => s.NextReviewDate)
            .Take(request.Limit)
            .ToListAsync(ct);

        var dueQuestionIds = dueStates.Select(s => s.QuestionId).ToList();

        // If fewer than limit, pad with never-attempted questions
        var remaining = request.Limit - dueStates.Count;
        List<Guid> newQuestionIds = [];
        if (remaining > 0)
        {
            var attemptedIds = await db.UserQuestionStates
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => s.QuestionId)
                .ToListAsync(ct);

            newQuestionIds = await db.Questions
                .AsNoTracking()
                .Where(q => q.Status == QuestionStatus.Active && !attemptedIds.Contains(q.Id))
                .OrderBy(_ => EF.Functions.Random())
                .Take(remaining)
                .Select(q => q.Id)
                .ToListAsync(ct);
        }

        var allQuestionIds = dueQuestionIds.Concat(newQuestionIds).ToList();

        var questions = await db.Questions
            .AsNoTracking()
            .Include(q => q.AnswerOptions)
            .Where(q => allQuestionIds.Contains(q.Id) && q.Status == QuestionStatus.Active)
            .ToListAsync(ct);

        // Batch presigned URLs
        var imageKeys = new List<string>();
        foreach (var q in questions)
        {
            if (q.ImageUrl is not null) imageKeys.Add(q.ImageUrl);
            foreach (var a in q.AnswerOptions)
                if (a.ImageUrl is not null) imageKeys.Add(a.ImageUrl);
        }
        var urlMap = await storage.GetPresignedUrlsBatchAsync(imageKeys, ct);

        var stateMap = dueStates.ToDictionary(s => s.QuestionId);

        var dtos = questions.Select(q =>
        {
            stateMap.TryGetValue(q.Id, out var state);
            var imgUrl = q.ImageUrl is not null ? urlMap.GetValueOrDefault(q.ImageUrl) : null;

            var options = q.AnswerOptions
                .OrderBy(_ => Random.Shared.Next())
                .Select(a => new PracticeAnswerOptionDto(
                    a.Id, a.Text,
                    a.ImageUrl is not null ? urlMap.GetValueOrDefault(a.ImageUrl) : null))
                .ToList();

            return new ReviewQuestionDto(
                q.Id, q.Text, imgUrl,
                (int)(state?.LeitnerBox ?? Domain.Common.Enums.LeitnerBox.Box1),
                state?.NextReviewDate ?? now,
                options);
        }).ToList();

        return ApiResponse<List<ReviewQuestionDto>>.Ok(dtos);
    }
}
