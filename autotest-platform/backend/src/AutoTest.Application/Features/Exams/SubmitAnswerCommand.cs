using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Exams;

public record SubmitAnswerCommand(
    Guid SessionId,
    Guid SessionQuestionId,
    Guid SelectedAnswerId,
    int? TimeSpentSeconds = null) : IRequest<ApiResponse>;

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
    IDateTimeProvider dateTime,
    ILogger<SubmitAnswerCommandHandler> logger) : IRequestHandler<SubmitAnswerCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(SubmitAnswerCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse.Fail("SESSION_NOT_FOUND", "Exam session not found.");

        if (session.Status != Domain.Common.Enums.ExamStatus.InProgress)
            return ApiResponse.Fail("SESSION_NOT_ACTIVE", "Session is not active.");

        // Check expiry (skip for marathon)
        if (session.Mode != Domain.Common.Enums.ExamMode.Marathon
            && session.ExpiresAt.HasValue
            && session.ExpiresAt.Value < dateTime.UtcNow)
        {
            session.Status = Domain.Common.Enums.ExamStatus.Expired;
            await db.SaveChangesAsync(ct);
            return ApiResponse.Fail("SESSION_EXPIRED", "Exam session has expired.");
        }

        var sq = await db.SessionQuestions
            .Include(sq => sq.Question)
            .ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(sq => sq.Id == request.SessionQuestionId
                && sq.ExamSessionId == request.SessionId, ct);

        if (sq is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Session question not found.");

        if (sq.SelectedAnswerId.HasValue)
            return ApiResponse.Fail("ALREADY_ANSWERED", "Question already answered.");

        // Validate the answer belongs to this question
        var answer = sq.Question.AnswerOptions.FirstOrDefault(a => a.Id == request.SelectedAnswerId);
        if (answer is null)
            return ApiResponse.Fail("INVALID_ANSWER", "Answer option does not belong to this question.");

        sq.SelectedAnswerId = request.SelectedAnswerId;
        sq.IsCorrect = answer.IsCorrect;
        sq.TimeSpentSeconds = request.TimeSpentSeconds;
        sq.UpdatedAt = dateTime.UtcNow;

        // Save progress every 10 answers for marathon
        var answeredCount = await db.SessionQuestions
            .CountAsync(q => q.ExamSessionId == request.SessionId && q.SelectedAnswerId.HasValue, ct);

        if (session.Mode == Domain.Common.Enums.ExamMode.Marathon && answeredCount % 10 == 0)
            logger.LogDebug("Marathon progress: {Count} answered in session {SessionId}", answeredCount, request.SessionId);

        await db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}
