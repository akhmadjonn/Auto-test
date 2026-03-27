using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Leaderboard;

public record GetLeaderboardQuery(
    string Period = "weekly",
    int Limit = 50) : IRequest<ApiResponse<LeaderboardDto>>;

public record LeaderboardDto(
    List<LeaderboardEntryDto> Rankings,
    LeaderboardEntryDto? CurrentUser);

public record LeaderboardEntryDto(
    Guid UserId,
    string? FirstName,
    string? LastName,
    long XpValue,
    int Rank,
    int Level);

public class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    private static readonly string[] ValidPeriods = ["daily", "weekly", "alltime"];

    public GetLeaderboardQueryValidator()
    {
        RuleFor(x => x.Period).Must(p => ValidPeriods.Contains(p))
            .WithMessage("Period must be one of: daily, weekly, alltime");
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

public class GetLeaderboardQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ICacheService cacheService)
    : IRequestHandler<GetLeaderboardQuery, ApiResponse<LeaderboardDto>>
{
    public async Task<ApiResponse<LeaderboardDto>> Handle(GetLeaderboardQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<LeaderboardDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        // Cache rankings only (not CurrentUser — that's per-user)
        var cacheKey = $"avtolider:leaderboard:{request.Period}";
        var cachedRankings = await cacheService.GetAsync<List<LeaderboardEntryDto>>(cacheKey, ct);

        List<LeaderboardEntryDto> rankings;
        if (cachedRankings is not null)
        {
            rankings = cachedRankings;
        }
        else
        {
            rankings = await LoadRankingsAsync(request.Period, request.Limit, now, ct);

            // Assign ranks after materialization
            rankings = rankings.Select((r, i) => r with { Rank = i + 1 }).ToList();

            await cacheService.SetAsync(cacheKey, rankings, TimeSpan.FromMinutes(5), ct);
        }

        // Always compute current user rank per-request (not cached)
        var currentUserEntry = rankings.FirstOrDefault(r => r.UserId == userId);
        if (currentUserEntry is null)
            currentUserEntry = await GetUserRankAsync(userId, request.Period, now, ct);

        return ApiResponse<LeaderboardDto>.Ok(new LeaderboardDto(rankings, currentUserEntry));
    }

    private async Task<List<LeaderboardEntryDto>> LoadRankingsAsync(
        string period, int limit, DateTimeOffset now, CancellationToken ct)
    {
        if (period == "alltime")
        {
            return await db.Users
                .AsNoTracking()
                .Where(u => u.TotalXp > 0 && !u.IsBlocked)
                .OrderByDescending(u => u.TotalXp)
                .Take(limit)
                .Select(u => new LeaderboardEntryDto(
                    u.Id, u.FirstName, u.LastName, u.TotalXp, 0, u.Level))
                .ToListAsync(ct);
        }

        var startDate = period == "daily"
            ? DateOnly.FromDateTime(now.UtcDateTime)
            : DateOnly.FromDateTime(now.AddDays(-7).UtcDateTime);

        return await db.UserDailyStats
            .AsNoTracking()
            .Where(s => s.StatDate >= startDate)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, TotalXp = g.Sum(s => s.XpEarned) })
            .OrderByDescending(x => x.TotalXp)
            .Take(limit)
            .Join(db.Users.AsNoTracking(), x => x.UserId, u => u.Id,
                (x, u) => new LeaderboardEntryDto(
                    u.Id, u.FirstName, u.LastName, x.TotalXp, 0, u.Level))
            .ToListAsync(ct);
    }

    private async Task<LeaderboardEntryDto?> GetUserRankAsync(
        Guid userId, string period, DateTimeOffset now, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        long xpValue;
        int rank;

        if (period == "alltime")
        {
            xpValue = user.TotalXp;
            rank = await db.Users.AsNoTracking()
                .CountAsync(u => u.TotalXp > user.TotalXp && !u.IsBlocked, ct) + 1;
        }
        else
        {
            var startDate = period == "daily"
                ? DateOnly.FromDateTime(now.UtcDateTime)
                : DateOnly.FromDateTime(now.AddDays(-7).UtcDateTime);

            xpValue = await db.UserDailyStats.AsNoTracking()
                .Where(s => s.UserId == userId && s.StatDate >= startDate)
                .SumAsync(s => s.XpEarned, ct);

            // Use subquery approach to avoid complex GroupBy+CountAsync predicate
            var usersWithMoreXp = await db.UserDailyStats.AsNoTracking()
                .Where(s => s.StatDate >= startDate)
                .GroupBy(s => s.UserId)
                .Select(g => new { UserId = g.Key, TotalXp = g.Sum(s => s.XpEarned) })
                .Where(x => x.TotalXp > xpValue)
                .CountAsync(ct);

            rank = usersWithMoreXp + 1;
        }

        return new LeaderboardEntryDto(userId, user.FirstName, user.LastName, xpValue, rank, user.Level);
    }
}
