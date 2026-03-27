using AutoTest.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class StudyReminderService(
    IServiceScopeFactory scopeFactory,
    ITelegramNotificationService telegram,
    ILogger<StudyReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StudyReminderService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                await SendRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "StudyReminderService failed");
            }
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTime.UtcNow;
        var currentHour = now.Hour.ToString();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Get users with reminders enabled at this hour who have a TelegramId
        var usersToRemind = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.Key == "daily_reminder_enabled" && s.Value == "true")
            .Select(s => s.UserId)
            .ToListAsync(ct);

        if (usersToRemind.Count == 0)
            return;

        // Filter by reminder_hour setting
        var hourSettings = await db.UserSettings
            .AsNoTracking()
            .Where(s => usersToRemind.Contains(s.UserId) && s.Key == "reminder_hour")
            .ToDictionaryAsync(s => s.UserId, s => s.Value, ct);

        // Get users with Telegram IDs
        var users = await db.Users
            .AsNoTracking()
            .Where(u => usersToRemind.Contains(u.Id) && u.TelegramId.HasValue)
            .Select(u => new { u.Id, u.TelegramId, u.CurrentStreak, u.LastStudyDate })
            .ToListAsync(ct);

        var sentCount = 0;
        var failCount = 0;

        foreach (var user in users)
        {
            // Check if hour matches (default 9 if not set)
            var reminderHour = hourSettings.GetValueOrDefault(user.Id, "9");
            if (reminderHour != currentHour)
                continue;

            // Skip if already studied today
            if (user.LastStudyDate.HasValue)
            {
                var lastStudyDate = DateOnly.FromDateTime(user.LastStudyDate.Value.UtcDateTime);
                if (lastStudyDate == today)
                    continue;
            }

            var message = $"Bugun mashq qildingizmi? 📚 Seriya: {user.CurrentStreak} kun 🔥";

            try
            {
                await telegram.SendMessageAsync(user.TelegramId!.Value, message, ct);
                sentCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                logger.LogWarning(ex, "Failed to send study reminder to user {UserId}", user.Id);
            }
        }

        logger.LogInformation("StudyReminderService sent {Sent} reminders, {Failed} failed",
            sentCount, failCount);
    }
}
