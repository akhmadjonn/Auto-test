using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.AnimatedTest;

public record SubmitAnimatedSessionRequest(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TotalQuestions,
    int CorrectCount,
    int ScorePercentage,
    List<AnimatedAnswerRequest> Answers);

public record AnimatedAnswerRequest(
    string QuestionId,
    string SelectedOptionId,
    bool IsCorrect,
    string Category,
    int TimeSpentMs,
    DateTimeOffset AnsweredAt);

public record SubmitAnimatedSessionCommand(SubmitAnimatedSessionRequest Request)
    : IRequest<ApiResponse<SubmitAnimatedSessionResponse>>;

public record SubmitAnimatedSessionResponse(Guid SessionId);

public class SubmitAnimatedSessionCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ILogger<SubmitAnimatedSessionCommandHandler> logger) : IRequestHandler<SubmitAnimatedSessionCommand, ApiResponse<SubmitAnimatedSessionResponse>>
{
    public async Task<ApiResponse<SubmitAnimatedSessionResponse>> Handle(SubmitAnimatedSessionCommand command, CancellationToken ct)
    {
        try
        {
            if (currentUser.UserId is null)
                return ApiResponse<SubmitAnimatedSessionResponse>.Fail("UNAUTHORIZED", "Not authenticated.");

            var req = command.Request;

            var session = new AnimatedTestSession
            {
                UserId = currentUser.UserId.Value,
                StartedAt = req.StartedAt,
                CompletedAt = req.CompletedAt,
                TotalQuestions = (short)req.TotalQuestions,
                CorrectCount = (short)req.CorrectCount,
                ScorePercentage = (short)req.ScorePercentage,
                QuestionIds = req.Answers.Select(a => a.QuestionId).ToArray(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.AnimatedTestSessions.Add(session);

            foreach (var a in req.Answers)
            {
                db.AnimatedTestAnswers.Add(new AnimatedTestAnswer
                {
                    SessionId = session.Id,
                    QuestionId = a.QuestionId,
                    SelectedOptionId = a.SelectedOptionId,
                    IsCorrect = a.IsCorrect,
                    Category = a.Category,
                    TimeSpentMs = a.TimeSpentMs,
                    AnsweredAt = a.AnsweredAt,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation("Animated test session {SessionId} saved for user {UserId} — score {Score}%",
                session.Id, currentUser.UserId, req.ScorePercentage);

            return ApiResponse<SubmitAnimatedSessionResponse>.Ok(new SubmitAnimatedSessionResponse(session.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save animated test session for user {UserId}", currentUser.UserId);
            return ApiResponse<SubmitAnimatedSessionResponse>.Fail("SAVE_FAILED", "Failed to save session.");
        }
    }
}
