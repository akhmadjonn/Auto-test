using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class SessionExpirationService(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            await ExpireStaleSessionsAsync(ct);
        }
    }

    private async Task ExpireStaleSessionsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var now = DateTimeOffset.UtcNow;

            var expired = await db.ExamSessions
                .Where(s => s.Status == ExamStatus.InProgress
                    && s.ExpiresAt != null
                    && s.ExpiresAt < now
                    && s.Mode != ExamMode.Marathon)
                .ToListAsync(ct);

            if (expired.Count == 0)
                return;

            foreach (var session in expired)
                session.Status = ExamStatus.Expired;

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Expired {Count} stale exam sessions", expired.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error expiring stale sessions");
        }
    }
}
