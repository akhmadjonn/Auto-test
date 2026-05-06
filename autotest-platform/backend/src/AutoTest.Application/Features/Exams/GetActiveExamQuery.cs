using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

public record GetActiveExamQuery : IRequest<ApiResponse<ActiveExamDto?>>;

public record ActiveExamDto(
    Guid Id,
    string Mode,
    int TotalQuestions,
    int AnsweredQuestions,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    string Status,
    int? RemainingSecondsAtPause);

public class GetActiveExamQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetActiveExamQuery, ApiResponse<ActiveExamDto?>>
{
    public async Task<ApiResponse<ActiveExamDto?>> Handle(GetActiveExamQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<ActiveExamDto?>.Fail("UNAUTHORIZED", "Not authenticated.");

        var session = await db.ExamSessions
            .AsNoTracking()
            .Include(s => s.SessionQuestions)
            .Where(s => s.UserId == currentUser.UserId
                && (s.Status == ExamStatus.InProgress || s.Status == ExamStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session is null)
            return ApiResponse<ActiveExamDto?>.Ok(null);

        var mode = session.Mode switch
        {
            ExamMode.Exam => "exam",
            ExamMode.Ticket => "ticket",
            ExamMode.Marathon => "marathon",
            ExamMode.SpeedChallenge => "speedChallenge",
            _ => "exam"
        };

        var answered = session.SessionQuestions.Count(sq => sq.SelectedAnswerId is not null);

        var status = session.Status == ExamStatus.Paused ? "paused" : "inProgress";

        return ApiResponse<ActiveExamDto?>.Ok(new ActiveExamDto(
            session.Id,
            mode,
            session.SessionQuestions.Count,
            answered,
            session.ExpiresAt,
            session.CreatedAt,
            status,
            session.RemainingSecondsAtPause));
    }
}
