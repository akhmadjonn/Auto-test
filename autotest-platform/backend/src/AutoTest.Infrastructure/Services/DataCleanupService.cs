using System.Diagnostics;
using AutoTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class DataCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 5 minutes after startup before first cleanup run
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data cleanup cycle failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        logger.LogInformation("Data cleanup cycle started");
        var totalSw = Stopwatch.StartNew();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await CleanExpiredOtpRequestsAsync(db, ct);
        await CleanOldAuditLogsAsync(db, ct);
        await LogTableSizesAsync(db, ct);

        totalSw.Stop();
        logger.LogInformation("Data cleanup cycle completed in {ElapsedMs}ms", totalSw.ElapsedMilliseconds);
    }

    private async Task CleanExpiredOtpRequestsAsync(AppDbContext db, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

        var deleted = await db.OtpRequests
            .Where(o => o.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        sw.Stop();
        if (deleted > 0)
            logger.LogInformation("Cleaned {Count} expired OTP requests (older than 24h) in {ElapsedMs}ms",
                deleted, sw.ElapsedMilliseconds);
    }

    private async Task CleanOldAuditLogsAsync(AppDbContext db, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);

        var deleted = await db.AuditLogs
            .Where(a => a.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        sw.Stop();
        if (deleted > 0)
            logger.LogInformation("Cleaned {Count} old audit logs (older than 90d) in {ElapsedMs}ms",
                deleted, sw.ElapsedMilliseconds);
    }

    private async Task LogTableSizesAsync(AppDbContext db, CancellationToken ct)
    {
        var sessionQuestionCount = await db.SessionQuestions.LongCountAsync(ct);
        var userQuestionStateCount = await db.UserQuestionStates.LongCountAsync(ct);
        var examSessionCount = await db.ExamSessions.LongCountAsync(ct);
        var questionCount = await db.Questions.LongCountAsync(ct);

        logger.LogInformation(
            "Table sizes — SessionQuestions: {SQ}, UserQuestionStates: {UQS}, ExamSessions: {ES}, Questions: {Q}",
            sessionQuestionCount, userQuestionStateCount, examSessionCount, questionCount);
    }
}
