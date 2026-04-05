using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.AnimatedTest;

public record GetAnimatedSessionDetailQuery(Guid SessionId)
    : IRequest<ApiResponse<AnimatedSessionDetailDto>>;

public record AnimatedSessionDetailDto(
    Guid SessionId,
    int ScorePercentage,
    int CorrectCount,
    int TotalQuestions,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TimeTakenSeconds,
    List<AnimatedAnswerDto> Answers);

public record AnimatedAnswerDto(
    string QuestionId,
    string SelectedOptionId,
    bool IsCorrect,
    string Category,
    int TimeSpentMs,
    DateTimeOffset AnsweredAt);

public class GetAnimatedSessionDetailQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetAnimatedSessionDetailQuery, ApiResponse<AnimatedSessionDetailDto>>
{
    public async Task<ApiResponse<AnimatedSessionDetailDto>> Handle(GetAnimatedSessionDetailQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<AnimatedSessionDetailDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.AnimatedTestSessions
            .AsNoTracking()
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == currentUser.UserId, ct);

        if (session is null)
            return ApiResponse<AnimatedSessionDetailDto>.Fail("NOT_FOUND", "Session not found.");

        var answers = session.Answers
            .OrderBy(a => a.AnsweredAt)
            .Select(a => new AnimatedAnswerDto(
                a.QuestionId,
                a.SelectedOptionId,
                a.IsCorrect,
                a.Category,
                a.TimeSpentMs,
                a.AnsweredAt))
            .ToList();

        var dto = new AnimatedSessionDetailDto(
            session.Id,
            session.ScorePercentage,
            session.CorrectCount,
            session.TotalQuestions,
            session.StartedAt,
            session.CompletedAt,
            (int)(session.CompletedAt - session.StartedAt).TotalSeconds,
            answers);

        return ApiResponse<AnimatedSessionDetailDto>.Ok(dto);
    }
}
