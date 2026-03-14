using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record CompleteExamCommand(Guid SessionId, Language Language = Language.UzLatin)
    : IRequest<ApiResponse<ExamResultDto>>;

public record ExamResultDto(
    Guid ExamId,
    int TotalQuestions,
    int CorrectAnswers,
    int Score,
    int PassingScore,
    bool Passed,
    int? TimeTakenSeconds,
    DateTimeOffset? CompletedAt,
    List<ExamResultQuestionDto> Questions);

public record ExamResultQuestionDto(
    Guid QuestionId,
    LocalizedText Text,
    string? ImageUrl,
    LocalizedText? Explanation,
    Guid? SelectedAnswerId,
    Guid? CorrectAnswerId,
    bool? IsCorrect,
    int? TimeSpentSeconds,
    List<ExamResultOptionDto> AnswerOptions);

public record ExamResultOptionDto(Guid Id, LocalizedText Text, bool IsCorrect, string? ImageUrl);

public class CompleteExamCommandValidator : AbstractValidator<CompleteExamCommand>
{
    public CompleteExamCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}

public class CompleteExamCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage,
    IDateTimeProvider dateTime,
    ILogger<CompleteExamCommandHandler> logger) : IRequestHandler<CompleteExamCommand, ApiResponse<ExamResultDto>>
{
    // Leitner intervals in days
    private static readonly int[] LeitnerIntervals = [1, 2, 4, 8, 16];

    public async Task<ApiResponse<ExamResultDto>> Handle(CompleteExamCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamResultDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        var session = await db.ExamSessions
            .Include(s => s.SessionQuestions)
                .ThenInclude(sq => sq.Question)
                    .ThenInclude(q => q.AnswerOptions)
            .Include(s => s.ExamTemplate)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId, ct);

        if (session is null)
            return ApiResponse<ExamResultDto>.Fail("SESSION_NOT_FOUND", "Session not found.");

        if (session.Status == ExamStatus.Completed)
            return ApiResponse<ExamResultDto>.Fail("ALREADY_COMPLETED", "Session already completed.");

        var now = dateTime.UtcNow;
        var correctCount = session.SessionQuestions.Count(sq => sq.IsCorrect == true);
        var total = session.SessionQuestions.Count;
        var score = total > 0 ? (int)Math.Round(correctCount * 100.0 / total) : 0;
        var passingScore = session.ExamTemplate?.PassingScore ?? 80;
        int? timeTaken = session.Mode != ExamMode.Marathon
            ? (int)(now - session.CreatedAt).TotalSeconds
            : null;

        session.Status = ExamStatus.Completed;
        session.Score = score;
        session.CorrectAnswers = correctCount;
        session.CompletedAt = now;
        session.TimeTakenSeconds = timeTaken;
        session.UpdatedAt = now;

        // Update Leitner spaced repetition states and category stats sequentially
        // (DbContext is not thread-safe — cannot run in parallel)
        var sessionQuestionsList = session.SessionQuestions.ToList();
        await UpdateLeitnerStatesAsync(userId, sessionQuestionsList, now, ct);
        await UpdateCategoryStatsAsync(userId, sessionQuestionsList, ct);

        await db.SaveChangesAsync(ct);

        // Build result with correct answers + explanations
        var questionDtos = await Task.WhenAll(session.SessionQuestions
            .OrderBy(sq => sq.Order)
            .Select(async sq =>
            {
                var q = sq.Question;
                var imgUrl = q.ImageUrl is not null ? await storage.GetPresignedUrlAsync(q.ImageUrl, ct) : null;
                var correctOptionId = q.AnswerOptions.FirstOrDefault(a => a.IsCorrect)?.Id;

                var optDtos = await Task.WhenAll(q.AnswerOptions.Select(async a =>
                {
                    var optImg = a.ImageUrl is not null ? await storage.GetPresignedUrlAsync(a.ImageUrl, ct) : null;
                    return new ExamResultOptionDto(a.Id, a.Text, a.IsCorrect, optImg);
                }));

                return new ExamResultQuestionDto(
                    q.Id,
                    q.Text,
                    imgUrl,
                    q.Explanation,
                    sq.SelectedAnswerId,
                    correctOptionId,
                    sq.IsCorrect,
                    sq.TimeSpentSeconds,
                    [..optDtos]);
            }));

        logger.LogInformation("Session {SessionId} completed: {Correct}/{Total} ({Score}%)",
            session.Id, correctCount, total, score);

        return ApiResponse<ExamResultDto>.Ok(new ExamResultDto(
            session.Id, total, correctCount, score, passingScore,
            score >= passingScore, timeTaken, now, [..questionDtos]));
    }

    private async Task UpdateCategoryStatsAsync(
        Guid userId,
        List<SessionQuestion> sessionQuestions,
        CancellationToken ct)
    {
        // Group questions by category
        var categoryGroups = sessionQuestions
            .GroupBy(sq => sq.Question.CategoryId)
            .ToList();

        var categoryIds = categoryGroups.Select(g => g.Key).ToList();
        var existingStats = await db.UserCategoryStats
            .Where(s => s.UserId == userId && categoryIds.Contains(s.CategoryId))
            .ToDictionaryAsync(s => s.CategoryId, ct);

        foreach (var group in categoryGroups)
        {
            if (!existingStats.TryGetValue(group.Key, out var stat))
            {
                stat = new UserCategoryStat { UserId = userId, CategoryId = group.Key };
                db.UserCategoryStats.Add(stat);
            }

            stat.TotalAttempts += group.Count();
            stat.CorrectAttempts += group.Count(sq => sq.IsCorrect == true);
        }
    }

    private async Task UpdateLeitnerStatesAsync(
        Guid userId,
        List<SessionQuestion> sessionQuestions,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var questionIds = sessionQuestions.Select(sq => sq.QuestionId).ToList();
        var existingStates = await db.UserQuestionStates
            .Where(s => s.UserId == userId && questionIds.Contains(s.QuestionId))
            .ToDictionaryAsync(s => s.QuestionId, ct);

        foreach (var sq in sessionQuestions)
        {
            if (!existingStates.TryGetValue(sq.QuestionId, out var state))
            {
                state = new UserQuestionState
                {
                    UserId = userId,
                    QuestionId = sq.QuestionId,
                    LeitnerBox = LeitnerBox.Box1,
                    NextReviewDate = now.AddDays(1),
                    TotalAttempts = 0,
                    CorrectAttempts = 0
                };
                db.UserQuestionStates.Add(state);
            }

            state.TotalAttempts++;
            state.LastAttemptAt = now;

            if (sq.IsCorrect == true)
            {
                state.CorrectAttempts++;
                // Advance box (max Box5)
                var nextBox = (int)state.LeitnerBox < 5
                    ? state.LeitnerBox + 1
                    : LeitnerBox.Box5;
                state.LeitnerBox = nextBox;
                state.NextReviewDate = now.AddDays(LeitnerIntervals[(int)nextBox - 1]);
            }
            else
            {
                // Reset to Box1
                state.LeitnerBox = LeitnerBox.Box1;
                state.NextReviewDate = now.AddDays(LeitnerIntervals[0]);
            }
        }
    }
}
