using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Practice;

public record SubmitPracticeAnswerCommand(
    Guid QuestionId,
    Guid SelectedAnswerId,
    int? TimeSpentSeconds = null) : IRequest<ApiResponse<PracticeAnswerFeedbackDto>>;

public record PracticeAnswerFeedbackDto(
    bool IsCorrect,
    Guid CorrectAnswerId,
    LocalizedText Explanation,
    int NewLeitnerBox,
    DateTimeOffset NextReviewDate);

public class SubmitPracticeAnswerCommandValidator : AbstractValidator<SubmitPracticeAnswerCommand>
{
    public SubmitPracticeAnswerCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.SelectedAnswerId).NotEmpty();
    }
}

public class SubmitPracticeAnswerCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cacheService,
    ILogger<SubmitPracticeAnswerCommandHandler> logger)
    : IRequestHandler<SubmitPracticeAnswerCommand, ApiResponse<PracticeAnswerFeedbackDto>>
{
    // [1,2,4,8,16] days per box
    private static readonly int[] LeitnerIntervals = [1, 2, 4, 8, 16];

    public async Task<ApiResponse<PracticeAnswerFeedbackDto>> Handle(
        SubmitPracticeAnswerCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PracticeAnswerFeedbackDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var question = await db.Questions
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId && q.IsActive, ct);

        if (question is null)
            return ApiResponse<PracticeAnswerFeedbackDto>.Fail("QUESTION_NOT_FOUND", "Question not found.");

        var selectedOption = question.AnswerOptions.FirstOrDefault(a => a.Id == request.SelectedAnswerId);
        if (selectedOption is null)
            return ApiResponse<PracticeAnswerFeedbackDto>.Fail("ANSWER_NOT_FOUND", "Answer option not found.");

        var correctOption = question.AnswerOptions.First(a => a.IsCorrect);
        var isCorrect = selectedOption.IsCorrect;

        // Update Leitner spaced repetition state
        var state = await db.UserQuestionStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.QuestionId == request.QuestionId, ct);

        if (state is null)
        {
            state = new UserQuestionState
            {
                UserId = userId,
                QuestionId = request.QuestionId,
                LeitnerBox = LeitnerBox.Box1,
                NextReviewDate = now.AddDays(LeitnerIntervals[0])
            };
            db.UserQuestionStates.Add(state);
        }

        state.TotalAttempts++;
        state.LastAttemptAt = now;

        if (isCorrect)
        {
            state.CorrectAttempts++;
            // Advance box (max Box5)
            var nextBox = (int)state.LeitnerBox < 5
                ? (LeitnerBox)((int)state.LeitnerBox + 1)
                : LeitnerBox.Box5;
            state.LeitnerBox = nextBox;
            state.NextReviewDate = now.AddDays(LeitnerIntervals[(int)nextBox - 1]);
        }
        else
        {
            // Reset to Box1 on incorrect
            state.LeitnerBox = LeitnerBox.Box1;
            state.NextReviewDate = now.AddDays(LeitnerIntervals[0]);
        }

        // Update category stats with 0.85 EMA decay (recent answers weighted more)
        var catStat = await db.UserCategoryStats
            .FirstOrDefaultAsync(s => s.UserId == userId && s.CategoryId == question.CategoryId, ct);

        if (catStat is null)
        {
            catStat = new UserCategoryStat { UserId = userId, CategoryId = question.CategoryId };
            db.UserCategoryStats.Add(catStat);
        }

        catStat.TotalAttempts = (int)Math.Round(catStat.TotalAttempts * 0.85) + 1;
        catStat.CorrectAttempts = (int)Math.Round(catStat.CorrectAttempts * 0.85) + (isCorrect ? 1 : 0);

        await db.SaveChangesAsync(ct);

        // Invalidate dashboard and category performance caches
        await cacheService.RemoveAsync($"avtolider:dashboard:{userId}", ct);
        await cacheService.RemoveAsync($"avtolider:catperf:{userId}", ct);

        logger.LogDebug(
            "Practice answer: user={UserId} question={QuestionId} correct={IsCorrect} box={Box} nextReview={NextReview}",
            userId, request.QuestionId, isCorrect, state.LeitnerBox, state.NextReviewDate);

        return ApiResponse<PracticeAnswerFeedbackDto>.Ok(new PracticeAnswerFeedbackDto(
            isCorrect,
            correctOption.Id,
            question.Explanation,
            (int)state.LeitnerBox,
            state.NextReviewDate));
    }
}
