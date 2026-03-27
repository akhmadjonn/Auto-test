using System.Diagnostics;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class LeaderboardSnapshotService(
    IServiceScopeFactory scopeFactory,
    ILogger<LeaderboardSnapshotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LeaderboardSnapshotService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Use IDateTimeProvider for testability
            using (var scope = scopeFactory.CreateScope())
            {
                var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                var now = dateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;

                logger.LogDebug("LeaderboardSnapshotService sleeping until {NextRun}", nextMidnight);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await TakeSnapshotsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LeaderboardSnapshotService failed");
            }
        }
    }

    private async Task TakeSnapshotsAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        // Snapshot date = yesterday (we just crossed midnight, capturing the previous day)
        var yesterday = DateOnly.FromDateTime(dateTime.UtcNow.AddDays(-1).UtcDateTime);
        var today = DateOnly.FromDateTime(dateTime.UtcNow.UtcDateTime);

        // Deduplication: skip if already snapshotted for yesterday
        var alreadyExists = await db.LeaderboardSnapshots
            .AnyAsync(s => s.SnapshotDate == yesterday && s.Period == "alltime", ct);
        if (alreadyExists)
        {
            logger.LogInformation("LeaderboardSnapshotService skipped — snapshots for {Date} already exist", yesterday);
            return;
        }

        var count = 0;

        // Alltime top 50
        var allTimeTop = await db.Users.AsNoTracking()
            .Where(u => u.TotalXp > 0 && !u.IsBlocked)
            .OrderByDescending(u => u.TotalXp)
            .Take(50)
            .Select(u => new { u.Id, u.TotalXp })
            .ToListAsync(ct);

        for (var i = 0; i < allTimeTop.Count; i++)
        {
            db.LeaderboardSnapshots.Add(new LeaderboardSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = allTimeTop[i].Id,
                Period = "alltime",
                SnapshotDate = yesterday,
                Rank = i + 1,
                XpValue = allTimeTop[i].TotalXp
            });
        }
        count += allTimeTop.Count;

        // Weekly top 50 (7 days ending yesterday)
        var weekStart = yesterday.AddDays(-6);
        var weeklyTop = await db.UserDailyStats.AsNoTracking()
            .Where(s => s.StatDate >= weekStart && s.StatDate <= yesterday)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, TotalXp = g.Sum(s => s.XpEarned) })
            .OrderByDescending(x => x.TotalXp)
            .Take(50)
            .ToListAsync(ct);

        for (var i = 0; i < weeklyTop.Count; i++)
        {
            db.LeaderboardSnapshots.Add(new LeaderboardSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = weeklyTop[i].UserId,
                Period = "weekly",
                SnapshotDate = yesterday,
                Rank = i + 1,
                XpValue = weeklyTop[i].TotalXp
            });
        }
        count += weeklyTop.Count;

        // Daily top 50 (yesterday's stats)
        var dailyTop = await db.UserDailyStats.AsNoTracking()
            .Where(s => s.StatDate == yesterday)
            .OrderByDescending(s => s.XpEarned)
            .Take(50)
            .Select(s => new { s.UserId, TotalXp = s.XpEarned })
            .ToListAsync(ct);

        for (var i = 0; i < dailyTop.Count; i++)
        {
            db.LeaderboardSnapshots.Add(new LeaderboardSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = dailyTop[i].UserId,
                Period = "daily",
                SnapshotDate = yesterday,
                Rank = i + 1,
                XpValue = dailyTop[i].TotalXp
            });
        }
        count += dailyTop.Count;

        await db.SaveChangesAsync(ct);
        sw.Stop();

        logger.LogInformation("LeaderboardSnapshotService saved {Count} snapshots for {Date} in {Elapsed}ms",
            count, yesterday, sw.ElapsedMilliseconds);
    }
}
