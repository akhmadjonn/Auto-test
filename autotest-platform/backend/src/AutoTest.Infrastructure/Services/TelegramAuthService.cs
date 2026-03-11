using System.Security.Cryptography;
using System.Text;
using AutoTest.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AutoTest.Infrastructure.Services;

public class TelegramAuthService(IConfiguration configuration) : ITelegramAuthService
{
    // Telegram widget verification:
    // secret_key = SHA256(bot_token)
    // data_check_string = alphabetically sorted key=value pairs joined by \n
    // expected_hash = HMAC-SHA256(data_check_string, secret_key).ToHexString()
    public bool VerifyHash(long id, string firstName, string? lastName, string? username, string? photoUrl, long authDate, string hash)
    {
        var botToken = configuration["TelegramSettings:BotToken"];
        if (string.IsNullOrEmpty(botToken))
            return false;

        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        var pairs = new SortedDictionary<string, string>
        {
            ["auth_date"] = authDate.ToString(),
            ["first_name"] = firstName,
            ["id"] = id.ToString()
        };

        if (!string.IsNullOrEmpty(lastName)) pairs["last_name"] = lastName;
        if (!string.IsNullOrEmpty(username)) pairs["username"] = username;
        if (!string.IsNullOrEmpty(photoUrl)) pairs["photo_url"] = photoUrl;

        var dataCheckString = string.Join("\n", pairs.Select(kv => $"{kv.Key}={kv.Value}"));
        var expectedHash = Convert.ToHexString(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString)));

        return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
    }
}
