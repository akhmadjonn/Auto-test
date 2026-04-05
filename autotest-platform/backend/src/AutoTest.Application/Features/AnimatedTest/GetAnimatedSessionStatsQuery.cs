using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.AnimatedTest;

public record GetAnimatedSessionStatsQuery : IRequest<ApiResponse<AnimatedSessionStatsDto>>;

public record AnimatedSessionStatsDto(
    int TotalSessions,
    int AvgScore,
    int BestScore,
    Dictionary<string, CategoryStatDto> CategoryStats);

public record CategoryStatDto(int Total, int Correct, int Accuracy);

public class GetAnimatedSessionStatsQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetAnimatedSessionStatsQuery, ApiResponse<AnimatedSessionStatsDto>>
{
    public async Task<ApiResponse<AnimatedSessionStatsDto>> Handle(GetAnimatedSessionStatsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<AnimatedSessionStatsDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var sessions = await db.AnimatedTestSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .Select(s => new { s.ScorePercentage })
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return ApiResponse<AnimatedSessionStatsDto>.Ok(
                new AnimatedSessionStatsDto(0, 0, 0, new Dictionary<string, CategoryStatDto>()));
        }

        var totalSessions = sessions.Count;
        var avgScore = (int)sessions.Average(s => s.ScorePercentage);
        var bestScore = sessions.Max(s => (int)s.ScorePercentage);

        var categoryStats = await db.AnimatedTestAnswers
            .AsNoTracking()
            .Where(a => a.Session.UserId == currentUser.UserId)
            .GroupBy(a => a.Category)
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Count(),
                Correct = g.Count(a => a.IsCorrect)
            })
            .ToListAsync(ct);

        var categoryDict = categoryStats.ToDictionary(
            c => c.Category,
            c => new CategoryStatDto(c.Total, c.Correct, c.Total > 0 ? (int)Math.Round(100.0 * c.Correct / c.Total) : 0));

        return ApiResponse<AnimatedSessionStatsDto>.Ok(
            new AnimatedSessionStatsDto(totalSessions, avgScore, bestScore, categoryDict));
    }
}
