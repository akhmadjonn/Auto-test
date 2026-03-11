namespace AutoTest.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendPushNotificationAsync(Guid userId, string title, string body, CancellationToken ct = default);
    Task SendBulkNotificationAsync(IEnumerable<Guid> userIds, string title, string body, CancellationToken ct = default);
    Task SendStreakReminderAsync(Guid userId, int currentStreak, CancellationToken ct = default);
    Task SendSubscriptionExpiringAsync(Guid userId, int daysLeft, CancellationToken ct = default);
}
