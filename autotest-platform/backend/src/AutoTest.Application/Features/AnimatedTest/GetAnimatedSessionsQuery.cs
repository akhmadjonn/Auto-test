using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.AnimatedTest;

public record GetAnimatedSessionsQuery(int Page = 1, int PageSize = 10)
    : IRequest<ApiResponse<PaginatedList<AnimatedSessionListDto>>>;

public record AnimatedSessionListDto(
    Guid SessionId,
    int ScorePercentage,
    int CorrectCount,
    int TotalQuestions,
    DateTimeOffset CompletedAt,
    int TimeTakenSeconds);

public class GetAnimatedSessionsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetAnimatedSessionsQuery, ApiResponse<PaginatedList<AnimatedSessionListDto>>>
{
    public async Task<ApiResponse<PaginatedList<AnimatedSessionListDto>>> Handle(GetAnimatedSessionsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PaginatedList<AnimatedSessionListDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var query = db.AnimatedTestSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync(ct);

        var sessions = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new AnimatedSessionListDto(
                s.Id,
                s.ScorePercentage,
                s.CorrectCount,
                s.TotalQuestions,
                s.CompletedAt,
                (int)(s.CompletedAt - s.StartedAt).TotalSeconds))
            .ToListAsync(ct);

        return ApiResponse<PaginatedList<AnimatedSessionListDto>>.Ok(
            new PaginatedList<AnimatedSessionListDto>(sessions, total, request.Page, request.PageSize));
    }
}
