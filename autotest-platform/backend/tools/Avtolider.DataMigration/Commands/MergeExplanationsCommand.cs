using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using Avtolider.DataMigration.Models;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Backfills explanations from APK JSON into DB questions that have empty explanations.
/// Matches questions by normalized Russian text.
/// </summary>
public static class MergeExplanationsCommand
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║      MERGE EXPLANATIONS (APK→DB)     ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        Console.WriteLine();

        var apkDir = Path.Combine(ctx.DataPath, "apk");
        if (!Directory.Exists(apkDir))
        {
            Console.WriteLine($"  [INFO] APK data directory not found: {apkDir}");
            return;
        }

        // Load APK question files for explanation text
        var uzkiril = LoadApkJson(Path.Combine(apkDir, "uzkiril.json"));
        var uzlotin = LoadApkJson(Path.Combine(apkDir, "uzlotin.json"));
        var rus = LoadApkJson(Path.Combine(apkDir, "rus.json"));

        if (uzkiril is null || rus is null)
        {
            Console.WriteLine("  [ERROR] Required uzkiril.json and rus.json not found.");
            return;
        }

        // Build explanation lookup: normalized Russian text → (uz, uzLatin, ru) explanation
        var explanationMap = new Dictionary<string, LocalizedText>();
        var kirMap = uzkiril.ToDictionary(q => q.Id);
        var latMap = uzlotin?.ToDictionary(q => q.Id) ?? [];

        foreach (var ruEntry in rus)
        {
            var ruExpl = ruEntry.Description?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ruExpl))
                continue;

            kirMap.TryGetValue(ruEntry.Id, out var kirEntry);
            latMap.TryGetValue(ruEntry.Id, out var latEntry);

            var uzExpl = kirEntry?.Description?.Trim() ?? string.Empty;
            var latExpl = latEntry?.Description?.Trim()
                ?? (string.IsNullOrEmpty(uzExpl) ? string.Empty : UzbekTransliterator.ToLatin(uzExpl));

            var normalizedRu = UzbekTransliterator.Normalize(ruEntry.Question);
            if (normalizedRu.Length > 0 && !explanationMap.ContainsKey(normalizedRu))
                explanationMap[normalizedRu] = new LocalizedText(uzExpl, latExpl, ruExpl);
        }

        Console.WriteLine($"  Explanation sources loaded: {explanationMap.Count} questions with explanations");

        // Find DB questions with empty explanations
        // Load all active questions client-side, then filter — avoids JSONB translation issues
        var allActive = await ctx.Db.Questions
            .Where(q => q.Status == QuestionStatus.Active)
            .ToListAsync(ct);
        var questionsToUpdate = allActive
            .Where(q => string.IsNullOrEmpty(q.Explanation.Ru))
            .ToList();

        Console.WriteLine($"  Questions with empty explanations: {questionsToUpdate.Count}");

        // Clear tracker — we'll use raw SQL to update JSONB owned entities
        ctx.Db.ChangeTracker.Clear();

        int updated = 0, notFound = 0;
        foreach (var question in questionsToUpdate)
        {
            ct.ThrowIfCancellationRequested();

            var normalizedRu = UzbekTransliterator.Normalize(question.Text.Ru);
            if (explanationMap.TryGetValue(normalizedRu, out var explanation))
            {
                if (!ctx.DryRun)
                {
                    var json = JsonSerializer.Serialize(new { explanation.Uz, explanation.UzLatin, explanation.Ru });
                    await ctx.Db.Database.ExecuteSqlAsync(
                        $"""UPDATE autotest."Questions" SET "Explanation" = CAST({json} AS jsonb) WHERE "Id" = {question.Id}""", ct);
                }
                updated++;
            }
            else
                notFound++;
        }

        Console.WriteLine();
        Console.WriteLine($"  Merge done: {updated} explanations backfilled, {notFound} not matched");
    }

    private static List<ApkQuestion>? LoadApkJson(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<List<ApkQuestion>>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [ERROR] Failed to parse {path}: {ex.Message}");
            return null;
        }
    }
}
