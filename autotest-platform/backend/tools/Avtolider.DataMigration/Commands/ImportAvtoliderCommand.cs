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
/// Local images from data/avtolider/img/ are uploaded to MinIO (downloaded by prepare_data.py).
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
        Console.WriteLine("║  + local image upload                ║");
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
        var existingRuRaw = await ctx.Db.Questions
            .AsNoTracking()
            .Select(q => q.Text.Ru)
            .ToListAsync(ct);
        var existingRuTexts = new HashSet<string>(existingRuRaw.Select(UzbekTransliterator.Normalize));
        Console.WriteLine($"  Existing questions in DB: {existingRuTexts.Count}");
        Console.WriteLine();

        // Build local image map for downloaded images
        var imgDir = Path.Combine(avtoliderDir, "img");
        var localImageMap = BuildLocalImageMap(imgDir);
        Console.WriteLine($"  Local images found: {localImageMap.Count}");

        if (!ctx.DryRun && localImageMap.Count > 0)
            await ctx.ImageSvc.EnsureBucketAsync(ct);
        Console.WriteLine();

        var pendingRuTexts = new HashSet<string>(existingRuTexts);
        var batch = new List<Question>(ctx.BatchSize);
        int localImported = 0, localSkipped = 0, localImages = 0, localExternalImages = 0;
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

            // Try to upload local image (downloaded by prepare_data.py)
            string? imageKey = null, thumbKey = null;
            if (!string.IsNullOrEmpty(q.ImageUrl))
            {
                if (localImageMap.TryGetValue(q.Id.ToString(), out var localImgPath))
                {
                    if (!ctx.DryRun)
                    {
                        var result = await ctx.ImageSvc.UploadAsync(localImgPath, category.Slug, ct);
                        if (result is not null)
                        {
                            (imageKey, thumbKey) = result.Value;
                            localImages++;
                            ctx.Stats.RecordImageUploaded();
                        }
                    }
                    else
                        localImages++;
                }
                else
                {
                    localExternalImages++;
                }
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
                ImageUrl = imageKey,
                ThumbnailUrl = thumbKey,
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
                Console.WriteLine($"  Progress: {localImported} imported, {localSkipped} skipped, {localImages} images...");
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
        Console.WriteLine($"  Avtolider import done: {localImported} imported, {localSkipped} skipped, {localImages} images uploaded");
        if (localExternalImages > 0)
            Console.WriteLine($"  [INFO] {localExternalImages} questions had no local image (download missing or URL-only)");
    }

    // Explicit mapping: Avtolider theme_id → DbSeeder category slug
    // These must match the slugs in DbSeeder.SeedCategoriesAsync exactly
    private static readonly Dictionary<int, string> ThemeToSlugMap = new()
    {
        [2]  = "terms",                      // _1_  Термины
        [3]  = "participant-duties",         // _2_  Обязанности участников
        [4]  = "traffic-lights",             // _3_  Светофор и регулировщик
        [5]  = "warning-signals",            // _4_  Предупредительные и аварийные сигналы
        [26] = "vehicle-id-signs",           // _5_  Опознавательные знаки ТС
        [20] = "warning-signs",              // _6_  Предупреждающие знаки
        [29] = "priority-signs",             // _7_  Знаки приоритета
        [21] = "prohibitory-signs",          // _8_  Запрещающие знаки
        [22] = "mandatory-signs",            // _9_  Предписывающие знаки
        [23] = "informational-signs",        // _10_ Информационно-указательные, сервисные и доп. знаки
        [24] = "road-markings",              // _11_ Дорожные разметки
        [6]  = "starting-direction",         // _12_ Начало движения и изменение направления
        [7]  = "vehicle-positioning",        // _13_ Расположение ТС на проезжей части
        [8]  = "speed-limits",               // _14_ Скорость движения
        [10] = "parking",                    // _15_ Остановка и стоянка
        [9]  = "overtaking",                 // _16_ Обгон
        [11] = "equal-intersections",        // _17_ Равнозначные перекрёстки
        [13] = "unregulated-intersections",  // _18_ Нерегулируемые перекрёстки (со знаками приоритета)
        [12] = "regulated-intersections",    // _19_ Регулируемые перекрёстки (со светофором)
        [14] = "railway-crossings",          // _20_ Движение через железнодорожные пути
        [15] = "highway-driving",            // _21_ Движение по автомагистралям
        [16] = "external-lights",            // _22_ Внешние световые приборы
        [17] = "towing",                     // _23_ Буксировка
        [18] = "passenger-transport",        // _24_ Перевозка людей
        [19] = "cargo-transport",            // _25_ Перевозка грузов
        [25] = "technical-requirements",     // _26_ Условия запрещения эксплуатации ТС
        [27] = "driving-safety",             // _27_ Безопасность управления
        [28] = "first-aid",                  // _28_ Первая медицинская помощь
    };

    private static async Task<Dictionary<int, Category>> ImportThemesAsync(
        AppDbContext db,
        List<AvtoliderTheme> themes,
        MigrationContext ctx,
        CancellationToken ct)
    {
        var result = new Dictionary<int, Category>();

        // Load all seeded categories by slug for O(1) lookup
        var categoriesBySlug = await db.Categories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Slug, ct);

        foreach (var theme in themes)
        {
            if (!ThemeToSlugMap.TryGetValue(theme.Id, out var slug))
            {
                Console.WriteLine($"  [WARN] No slug mapping for theme_id={theme.Id} '{theme.NameRu}', skipping");
                continue;
            }

            if (!categoriesBySlug.TryGetValue(slug, out var category))
            {
                Console.WriteLine($"  [ERROR] Category slug '{slug}' not found in DB. Ensure DbSeeder has run. Theme: {theme.NameRu}");
                continue;
            }

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

    // Builds a lookup: question ID (string) → first matching local file path
    // Supports multiple extensions (prepare_data.py saves as .jpg, .png, or .webp)
    private static Dictionary<string, string> BuildLocalImageMap(string imgDirectory)
    {
        if (!Directory.Exists(imgDirectory))
            return [];

        return Directory.GetFiles(imgDirectory)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => Path.GetFileNameWithoutExtension(f))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(f => f).First());
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
