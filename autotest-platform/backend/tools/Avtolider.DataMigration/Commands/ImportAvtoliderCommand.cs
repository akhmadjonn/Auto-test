using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Models;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Imports questions from an Avtolider DB JSON export.
/// Expected files in data/avtolider/:
///   themes.json   — [{id, name_uz, name_ru}]
///   questions.json — [{id, question_uz, question_ru, image_url, theme_id, is_active}]
///   options.json   — [{id, quiz_id, text_uz, text_ru, is_correct}]
///
/// UzLatin is auto-generated via UzbekTransliterator if empty.
/// image_url (external URL) is logged but not downloaded — skipped for safety.
/// </summary>
public static class ImportAvtoliderCommand
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║  IMPORT AVTOLIDER (1044 questions)   ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        Console.WriteLine();

        var avtoliderDir = Path.Combine(ctx.DataPath, "avtolider");
        if (!Directory.Exists(avtoliderDir))
        {
            Console.WriteLine($"  [INFO] Avtolider data directory not found: {avtoliderDir}");
            Console.WriteLine("  Skipping. To use, place themes.json, questions.json, options.json in data/avtolider/");
            return;
        }

        // --- Load JSON files ---
        var themes = LoadJson<List<AvtoliderTheme>>(Path.Combine(avtoliderDir, "themes.json"));
        var questions = LoadJson<List<AvtoliderQuestion>>(Path.Combine(avtoliderDir, "questions.json"));
        var options = LoadJson<List<AvtoliderOption>>(Path.Combine(avtoliderDir, "options.json"));

        if (themes is null || questions is null || options is null)
        {
            Console.WriteLine("  [ERROR] All three files (themes.json, questions.json, options.json) are required.");
            return;
        }

        Console.WriteLine($"  Loaded: {themes.Count} themes, {questions.Count} questions, {options.Count} options");

        // Group options by quiz_id for O(1) lookup
        var optionsByQuizId = options
            .GroupBy(o => o.QuizId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Id).ToList());

        // --- Import themes as categories ---
        var categoryMap = await ImportThemesAsync(ctx.Db, themes, ctx, ct);
        Console.WriteLine($"  Categories processed: {categoryMap.Count}");

        // --- Load existing Russian texts for idempotency ---
        var existingRuList = await ctx.Db.Questions
            .AsNoTracking()
            .Select(q => UzbekTransliterator.Normalize(q.Text.Ru))
            .ToListAsync(ct);
        var existingRuTexts = new HashSet<string>(existingRuList);
        Console.WriteLine($"  Existing questions in DB: {existingRuTexts.Count}");
        Console.WriteLine();

        var pendingRuTexts = new HashSet<string>(existingRuTexts);
        var batch = new List<Question>(ctx.BatchSize);
        int localImported = 0, localSkipped = 0, localExternalImages = 0;
        int ticketCounter = 1; // global sequential ticket assignment for Avtolider

        foreach (var q in questions.OrderBy(q => q.Id))
        {
            ct.ThrowIfCancellationRequested();

            // Skip if no options
            if (!optionsByQuizId.TryGetValue(q.Id, out var qOptions) || qOptions.Count == 0)
            {
                Console.WriteLine($"  [WARN] Question ID={q.Id} has no options, skipping");
                localSkipped++;
                continue;
            }

            // Validate: exactly one correct answer
            if (!qOptions.Any(o => o.IsCorrect))
            {
                Console.WriteLine($"  [WARN] Question ID={q.Id}: no correct option, skipping");
                localSkipped++;
                continue;
            }

            var ruText = q.QuestionRu?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ruText))
            {
                localSkipped++;
                continue;
            }

            // Idempotency check
            var normalizedRu = UzbekTransliterator.Normalize(ruText);
            if (pendingRuTexts.Contains(normalizedRu))
            {
                localSkipped++;
                continue;
            }

            // Resolve category
            if (!categoryMap.TryGetValue(q.ThemeId, out var category))
            {
                Console.WriteLine($"  [WARN] Unknown theme_id={q.ThemeId} for question ID={q.Id}, skipping");
                localSkipped++;
                continue;
            }

            // Build trilingual text (UzLatin auto-generated from Cyrillic)
            var uzText = q.QuestionUz?.Trim() ?? string.Empty;
            var uzLatin = string.IsNullOrEmpty(uzText)
                ? string.Empty
                : UzbekTransliterator.ToLatin(uzText);

            // External image_url: we log and skip (cannot safely download arbitrary URLs)
            if (!string.IsNullOrEmpty(q.ImageUrl))
            {
                localExternalImages++;
                Console.WriteLine($"  [INFO] Q ID={q.Id} has external image_url (skipped): {q.ImageUrl}");
            }

            // Difficulty from option count
            var difficulty = qOptions.Count switch
            {
                <= 3 => Difficulty.Easy,
                4 => Difficulty.Medium,
                _ => Difficulty.Hard
            };

            var question = new Question
            {
                Id = Guid.NewGuid(),
                Text = new LocalizedText(uzText, uzLatin, ruText),
                Explanation = new LocalizedText(string.Empty, string.Empty, string.Empty),
                Difficulty = difficulty,
                CategoryId = category.Id,
                ImageUrl = null,  // external URL not downloaded
                ThumbnailUrl = null,
                LicenseCategory = LicenseCategory.AB,
                IsActive = q.IsActive,
                TicketNumber = (ticketCounter - 1) / 20 + 1,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Build answer options
            for (int i = 0; i < qOptions.Count; i++)
            {
                var opt = qOptions[i];
                var optUzText = opt.TextUz?.Trim() ?? string.Empty;
                var optUzLatin = string.IsNullOrEmpty(optUzText)
                    ? string.Empty
                    : UzbekTransliterator.ToLatin(optUzText);

                question.AnswerOptions.Add(new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    Text = new LocalizedText(
                        optUzText,
                        optUzLatin,
                        opt.TextRu?.Trim() ?? string.Empty),
                    IsCorrect = opt.IsCorrect,
                    SortOrder = i,
                    QuestionId = question.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            batch.Add(question);
            pendingRuTexts.Add(normalizedRu);
            ticketCounter++;

            if (batch.Count >= ctx.BatchSize)
            {
                var saved = await SaveBatchAsync(ctx.Db, batch, ctx.DryRun, ct);
                localImported += saved;
                Console.WriteLine($"  Progress: {localImported} imported, {localSkipped} skipped...");
                batch.Clear();
            }
        }

        // Flush remaining
        if (batch.Count > 0)
        {
            var saved = await SaveBatchAsync(ctx.Db, batch, ctx.DryRun, ct);
            localImported += saved;
        }

        ctx.Stats.RecordImported(localImported);
        ctx.Stats.RecordSkipped(localSkipped);

        Console.WriteLine();
        Console.WriteLine($"  Avtolider import done: {localImported} imported, {localSkipped} skipped");
        if (localExternalImages > 0)
            Console.WriteLine($"  [INFO] {localExternalImages} external image URLs encountered (not downloaded)");
    }

    private static async Task<Dictionary<int, Category>> ImportThemesAsync(
        AppDbContext db,
        List<AvtoliderTheme> themes,
        MigrationContext ctx,
        CancellationToken ct)
    {
        var result = new Dictionary<int, Category>();

        // Load existing slugs to detect conflicts
        var existingSlugList = await db.Categories
            .AsNoTracking()
            .Select(c => c.Slug)
            .ToListAsync(ct);
        var existingSlugs = new HashSet<string>(existingSlugList);

        int sortOrder = 200; // start after APK categories
        foreach (var theme in themes)
        {
            var baseSlug = SlugifyUz(theme.NameRu ?? theme.NameUz ?? string.Empty, $"theme-{theme.Id}");
            var slug = baseSlug;

            // Make unique if collision
            int suffix = 2;
            while (existingSlugs.Contains(slug))
                slug = $"{baseSlug}-{suffix++}";

            // Check if already exists by slug (from previous run)
            var existing = await db.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug, ct);

            if (existing is not null)
            {
                result[theme.Id] = existing;
                continue;
            }

            var uzName = theme.NameUz?.Trim() ?? string.Empty;
            var ruName = theme.NameRu?.Trim() ?? string.Empty;
            var uzLatinName = string.IsNullOrEmpty(uzName)
                ? string.Empty
                : UzbekTransliterator.ToLatin(uzName);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = new LocalizedText(uzName, uzLatinName, ruName),
                Description = new LocalizedText(string.Empty, string.Empty, string.Empty),
                Slug = slug,
                IsActive = true,
                SortOrder = sortOrder++,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            if (!ctx.DryRun)
            {
                db.Categories.Add(category);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }

            existingSlugs.Add(slug);
            result[theme.Id] = category;
        }

        return result;
    }

    private static async Task<int> SaveBatchAsync(
        AppDbContext db,
        List<Question> batch,
        bool dryRun,
        CancellationToken ct)
    {
        if (dryRun)
            return batch.Count;

        try
        {
            await db.Questions.AddRangeAsync(batch, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            return batch.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARN] Batch failed: {ex.Message}. Retrying individually...");
            db.ChangeTracker.Clear();

            int saved = 0;
            foreach (var question in batch)
            {
                try
                {
                    db.Questions.Add(question);
                    await db.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();
                    saved++;
                }
                catch (Exception innerEx)
                {
                    db.ChangeTracker.Clear();
                    var preview = question.Text.Ru.Length > 60
                        ? question.Text.Ru[..60] + "..."
                        : question.Text.Ru;
                    Console.WriteLine($"  [ERROR] Failed to save '{preview}': {innerEx.Message}");
                }
            }
            return saved;
        }
    }

    // Simple slug: lowercase, replace spaces with hyphens, remove non-alphanumeric
    private static string SlugifyUz(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var sb = new System.Text.StringBuilder();
        bool prevHyphen = false;
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                prevHyphen = false;
            }
            else if ((c == ' ' || c == '-' || c == '_') && !prevHyphen && sb.Length > 0)
            {
                sb.Append('-');
                prevHyphen = true;
            }
        }
        var result = sb.ToString().TrimEnd('-');
        return string.IsNullOrEmpty(result) ? fallback : result[..Math.Min(result.Length, 80)];
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  [WARN] Not found: {path}");
            return default;
        }
        try
        {
            var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [ERROR] Failed to parse {path}: {ex.Message}");
            return default;
        }
    }
}
