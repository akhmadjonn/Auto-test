using System.Text;
using System.Text.Json;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Services;

public class TelegramNotificationService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TelegramNotificationService> logger) : ITelegramNotificationService
{
    public async Task SendMessageAsync(long telegramId, string message, CancellationToken ct = default)
    {
        var botToken = configuration["TelegramSettings:BotToken"];
        if (string.IsNullOrEmpty(botToken))
        {
            logger.LogWarning("Telegram bot token not configured, skipping notification");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("Telegram");
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = JsonSerializer.Serialize(new
            {
                chat_id = telegramId,
                text = message,
                parse_mode = "HTML"
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Telegram API returned {StatusCode} for chat {ChatId}",
                    response.StatusCode, telegramId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Telegram message to {ChatId}", telegramId);
        }
    }
}
