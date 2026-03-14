using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Exams;

public record GetExamHistoryQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PaginatedList<ExamHistoryDto>>>;

public record ExamHistoryDto(
    Guid ExamId,
    int Score,
    int TotalQuestions,
    bool Passed,
    DateTimeOffset CompletedAt,
    int TimeTakenSeconds);

public class GetExamHistoryQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetExamHistoryQuery, ApiResponse<PaginatedList<ExamHistoryDto>>>
{
    public async Task<ApiResponse<PaginatedList<ExamHistoryDto>>> Handle(GetExamHistoryQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PaginatedList<ExamHistoryDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var query = db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId
                && s.Status == ExamStatus.Completed
                && s.CompletedAt.HasValue)
            .OrderByDescending(s => s.CompletedAt);

        var total = await query.CountAsync(ct);

        var sessions = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Score,
                TotalQuestions = s.SessionQuestions.Count,
                s.TimeTakenSeconds,
                s.CompletedAt,
                PassingScore = s.ExamTemplate != null ? s.ExamTemplate.PassingScore : 80
            })
            .ToListAsync(ct);

        var dtos = sessions.Select(s => new ExamHistoryDto(
            s.Id,
            s.Score ?? 0,
            s.TotalQuestions,
            (s.Score ?? 0) >= s.PassingScore,
            s.CompletedAt!.Value,
            s.TimeTakenSeconds ?? 0)).ToList();

        return ApiResponse<PaginatedList<ExamHistoryDto>>.Ok(
            new PaginatedList<ExamHistoryDto>(dtos, total, request.Page, request.PageSize));
    }
}
