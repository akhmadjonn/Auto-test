namespace AutoTest.Application.Common.Interfaces;

public interface ITelegramNotificationService
{
    Task SendMessageAsync(long telegramId, string message, CancellationToken ct = default);
}
