using AutoTest.Domain.Common.Enums;

namespace AutoTest.Application.Common.Constants;

public static class SmsTemplates
{
    // Web OTP: last line @domain #code enables browser autocomplete="one-time-code"
    // Android: <#> prefix + 11-char app hash (appended at runtime from config)
    // iOS: @domain #code line triggers keyboard autofill

    private const string OtpUzLatin =
    """
    Avtolider - tasdiqlash kodingiz: {code}
    Kod 5 daqiqa amal qiladi. Hech kimga bermang!
    @avtolider.uz #{code}
    """;

private const string OtpUzCyrillic =
    """
    Avtolider — тасдиқлаш кодингиз: {code}
    Код 5 дақиқа амал қилади. Ҳеч кимга берманг!
    @avtolider.uz #{code}
    """;

private const string OtpRu =
    """
    Avtolider — ваш код подтверждения: {code}
    Код действителен 5 минут. Никому не сообщайте!
    @avtolider.uz #{code}
    """;
    
    public static string FormatOtp(string code, Language language, string? androidAppHash = null)
    {
        var template = language switch
        {
            Language.Uz => OtpUzCyrillic,
            Language.UzLatin => OtpUzLatin,
            Language.Ru => OtpRu,
            _ => OtpUzLatin
        };

        var message = template.Replace("{code}", code).Trim();

        // Android SMS Retriever: prepend <#> and append 11-char app hash
        if (!string.IsNullOrEmpty(androidAppHash))
            message = $"<#> {message}\n{androidAppHash}";

        return message;
    }
}
