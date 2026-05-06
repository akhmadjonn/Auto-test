using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record SubmitAnswerCommand(
    Guid SessionId,
    Guid SessionQuestionId,
    Guid SelectedAnswerId,
    int? TimeSpentSeconds = null) : IRequest<ApiResponse<ExamAnswerFeedbackDto>>;

// Explanation populated only for Marathon — exam/ticket/speed-challenge surface a verdict
// without revealing the explanation mid-session; full explanations show on the result page.
public record ExamAnswerFeedbackDto(
    bool IsCorrect,
    Guid CorrectAnswerId,
    LocalizedText? Explanation);

public class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.SessionQuestionId).NotEmpty();
        RuleFor(x => x.SelectedAnswerId).NotEmpty();
    }
}

public class SubmitAnswerCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDistributedLockService lockService,
    IDateTimeProvider dateTime,
    IXpService xpService,
    ILogger<SubmitAnswerCommandHandler> logger) : IRequestHandler<SubmitAnswerCommand, ApiResponse<ExamAnswerFeedbackDto>>
{
    public async Task<ApiResponse<ExamAnswerFeedbackDto>> Handle(SubmitAnswerCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("SESSION_NOT_FOUND", "Exam session not found.");

        if (session.Status != ExamStatus.InProgress)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("SESSION_NOT_ACTIVE", "Session is not active.");

        // Check expiry (skip for marathon)
        if (session.Mode != ExamMode.Marathon
            && session.ExpiresAt.HasValue
            && session.ExpiresAt.Value < dateTime.UtcNow)
        {
            session.Status = ExamStatus.Expired;
            await db.SaveChangesAsync(ct);
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("SESSION_EXPIRED", "Exam session has expired.");
        }

        // Distributed lock prevents double-submit on the same question
        await using var lockHandle = await lockService.TryAcquireAsync(
            $"avtolider:lock:answer:{request.SessionQuestionId}", TimeSpan.FromSeconds(5), ct);
        if (lockHandle is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("CONCURRENT_REQUEST", "Answer submission in progress.");

        var sq = await db.SessionQuestions
            .Include(sq => sq.Question)
            .ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(sq => sq.Id == request.SessionQuestionId
                && sq.ExamSessionId == request.SessionId, ct);

        if (sq is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("QUESTION_NOT_FOUND", "Session question not found.");

        var correctOption = sq.Question.AnswerOptions.FirstOrDefault(a => a.IsCorrect);
        if (correctOption is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("DATA_INTEGRITY", "Question has no correct answer configured.");

        var includeExplanation = session.Mode == ExamMode.Marathon;

        // Lock-first-answer (idempotent): once a user has answered a question, return the
        // original verdict without re-scoring or re-awarding XP. Prevents score manipulation
        // after the user sees the correct answer reveal.
        if (sq.SelectedAnswerId is not null)
        {
            var firstWasCorrect = sq.IsCorrect ?? false;
            return ApiResponse<ExamAnswerFeedbackDto>.Ok(new ExamAnswerFeedbackDto(
                firstWasCorrect,
                correctOption.Id,
                includeExplanation ? sq.Question.Explanation : null));
        }

        // Validate the answer belongs to this question
        var answer = sq.Question.AnswerOptions.FirstOrDefault(a => a.Id == request.SelectedAnswerId);
        if (answer is null)
            return ApiResponse<ExamAnswerFeedbackDto>.Fail("INVALID_ANSWER", "Answer option does not belong to this question.");

        sq.SelectedAnswerId = request.SelectedAnswerId;
        sq.IsCorrect = answer.IsCorrect;
        sq.TimeSpentSeconds = request.TimeSpentSeconds;
        sq.UpdatedAt = dateTime.UtcNow;

        // Save progress every 10 answers for marathon
        var answeredCount = await db.SessionQuestions
            .CountAsync(q => q.ExamSessionId == request.SessionId && q.SelectedAnswerId.HasValue, ct);

        if (session.Mode == ExamMode.Marathon && answeredCount % 10 == 0)
            logger.LogDebug("Marathon progress: {Count} answered in session {SessionId}", answeredCount, request.SessionId);

        await db.SaveChangesAsync(ct);

        // Award XP based on answer correctness and exam mode
        var xpAmount = answer.IsCorrect
            ? session.Mode switch
            {
                ExamMode.HardMode => Common.Constants.XpRewards.HardModeCorrect,
                ExamMode.SpeedChallenge => Common.Constants.XpRewards.SpeedChallengeCorrect,
                ExamMode.Review => Common.Constants.XpRewards.ReviewCorrect,
                _ => Common.Constants.XpRewards.CorrectAnswer
            }
            : Common.Constants.XpRewards.IncorrectAnswer;

        await xpService.AwardXpAsync(currentUser.UserId.Value, xpAmount, "exam_answer", ct);
        await xpService.RecordAnswerAsync(currentUser.UserId.Value, answer.IsCorrect, request.TimeSpentSeconds, ct);
        await xpService.UpdateStreakAsync(currentUser.UserId.Value, ct);
        await db.SaveChangesAsync(ct);

        return ApiResponse<ExamAnswerFeedbackDto>.Ok(new ExamAnswerFeedbackDto(
            answer.IsCorrect,
            correctOption.Id,
            includeExplanation ? sq.Question.Explanation : null));
    }
}
