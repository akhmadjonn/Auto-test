using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Common.Services;

public record LocalizationEntry
{
    public int Code { get; init; }
    public string Language { get; init; } = "uz";
    public short HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
}

// Loads error localizations from a pipe-delimited CSV:
//   errorCode|language|httpStatusCode|localizedMessage
// Thread-safe read after initial load. Register as singleton.
public class LocalizationService(ILogger<LocalizationService> logger)
{
    private readonly ConcurrentBag<LocalizationEntry> _entries = [];

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            logger.LogError("Localization CSV not found at {Path}", path);
            return;
        }

        var lines = File.ReadAllLines(path);
        var loaded = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var parts = line.Split('|');
            if (parts.Length < 4)
            {
                logger.LogWarning("Skipping malformed localization line: {Line}", line);
                continue;
            }

            if (!int.TryParse(parts[0].Trim(), out var code)) continue;
            if (!short.TryParse(parts[2].Trim(), out var http)) continue;

            _entries.Add(new LocalizationEntry
            {
                Code = code,
                Language = parts[1].Trim().ToLowerInvariant(),
                HttpStatusCode = http,
                Message = string.Join('|', parts.Skip(3)).Trim(),
            });
            loaded++;
        }

        logger.LogInformation("Loaded {Count} localization entries from {Path}", loaded, path);
    }

    public LocalizationEntry GetEntry(int code, string language)
    {
        var lang = (language ?? "uz").ToLowerInvariant();

        var entry = _entries.FirstOrDefault(e => e.Code == code && e.Language == lang);
        if (entry is not null) return entry;

        // Fallback: try Uzbek, then English
        entry = _entries.FirstOrDefault(e => e.Code == code && e.Language == "uz")
             ?? _entries.FirstOrDefault(e => e.Code == code && e.Language == "en");
        if (entry is not null) return entry;

        return new LocalizationEntry
        {
            Code = code,
            Language = lang,
            HttpStatusCode = 400,
            Message = $"Unknown error: {code}",
        };
    }
}
