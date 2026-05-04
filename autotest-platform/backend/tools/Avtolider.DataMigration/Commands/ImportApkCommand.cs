using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Models;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Avtolider.DataMigration.Commands;

public static class ImportApkCommand
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║       IMPORT APK (700 questions)     ║");
        Console.WriteLine($"╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        Console.WriteLine();

        var apkDir = Path.Combine(ctx.DataPath, "apk");
        if (!Directory.Exists(apkDir))
        {
            Console.WriteLine($"  [ERROR] APK data directory not found: {apkDir}");
            return;
        }

        // --- Load all 3 language files ---
        var uzkiril = LoadJson(Path.Combine(apkDir, "uzkiril.json"));
        var uzlotin = LoadJson(Path.Combine(apkDir, "uzlotin.json"));
        var rus = LoadJson(Path.Combine(apkDir, "rus.json"));

        if (uzkiril is null || rus is null)
        {
            Console.WriteLine("  [ERROR] Required files uzkiril.json and rus.json must exist.");
            return;
        }

        Console.WriteLine($"  Loaded: uzkiril={uzkiril.Count}, uzlotin={uzlotin?.Count ?? 0}, rus={rus.Count}");

        // Index by ID for O(1) lookup
        var kirMap = uzkiril.ToDictionary(q => q.Id);
        var latMap = uzlotin?.ToDictionary(q => q.Id) ?? [];
        var ruMap = rus.ToDictionary(q => q.Id);

        // --- Build image lookup ---
        var imgDir = Path.Combine(apkDir, "img");
        var imgMap = ImageMigrationService.BuildImageMap(imgDir);
        Console.WriteLine($"  Image files found: {imgMap.Count}");

        if (!ctx.DryRun)
            await ctx.ImageSvc.EnsureBucketAsync(ct);

        // --- Get or create default APK category ---
        var category = await GetOrCreateApkCategoryAsync(ctx.Db, ctx, ct);
        Console.WriteLine($"  Category: '{category.Name.Ru}' (id={category.Id})");

        // --- Load existing Russian texts for idempotency ---
        var existingRuRaw = await ctx.Db.Questions
            .AsNoTracking()
            .Select(q => q.Text.Ru)
            .ToListAsync(ct);
        // Smart dedup: text-only on text alone, image-bearing on text+image-source.
        // See ImportAvtoliderCommand.BuildDedupKey for the same convention.
        var pendingDedupKeys = new HashSet<string>(existingRuRaw.Select(UzbekTransliterator.Normalize));
        Console.WriteLine($"  Existing question texts in DB: {pendingDedupKeys.Count}");
        Console.WriteLine();

        var batch = new List<Question>(ctx.BatchSize);

        int localImported = 0, localSkipped = 0, localImages = 0;

        foreach (var (id, kirEntry) in kirMap.OrderBy(kv => kv.Key))
        {
            ct.ThrowIfCancellationRequested();

            if (!ruMap.TryGetValue(id, out var ruEntry))
            {
                Console.WriteLine($"  [WARN] No Russian entry for ID={id}, skipping");
                localSkipped++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(ruEntry.Question))
            {
                localSkipped++;
                continue;
            }

            // For APK, the "image source" identifier is the Media.Name (a numeric
            // file stem) — distinct image stems = distinct picture-questions even
            // when wording matches (PO-confirmed).
            var apkImageRef = kirEntry.Media.Exist ? kirEntry.Media.Name : null;
            var dedupKey = ImportAvtoliderCommand.BuildDedupKey(ruEntry.Question, apkImageRef);
            if (pendingDedupKeys.Contains(dedupKey))
            {
                localSkipped++;
                continue;
            }

            // Build trilingual text
            latMap.TryGetValue(id, out var latEntry);
            var uzText = kirEntry.Question;
            var uzLatin = latEntry?.Question ?? UzbekTransliterator.ToLatin(kirEntry.Question);
            var ruText = ruEntry.Question;

            var uzExpl = kirEntry.Description ?? string.Empty;
            var ruExpl = ruEntry.Description ?? string.Empty;
            var latExpl = latEntry?.Description
                ?? (string.IsNullOrEmpty(uzExpl) ? string.Empty : UzbekTransliterator.ToLatin(uzExpl));

            // Difficulty from choice count (use kirEntry as canonical)
            var choiceCount = kirEntry.Choises.Count;
            var difficulty = choiceCount switch
            {
                <= 3 => Difficulty.Easy,
                4 => Difficulty.Medium,
                _ => Difficulty.Hard
            };

            // Image upload (skip in dry-run)
            string? imageKey = null, thumbKey = null;
            if (kirEntry.Media.Exist && imgMap.TryGetValue(kirEntry.Media.Name, out var imgPath))
            {
                if (!ctx.DryRun)
                {
                    var result = await ctx.ImageSvc.UploadAsync(imgPath, category.Slug, ct);
                    if (result is not null)
                    {
                        (imageKey, thumbKey) = result.Value;
                        localImages++;
                        ctx.Stats.RecordImageUploaded();
                    }
                }
                else
                    localImages++; // count as "would upload"
            }
            else if (kirEntry.Media.Exist)
                Console.WriteLine($"  [WARN] No image file for ID={id}, media.name='{kirEntry.Media.Name}'");

            // Build Question entity.
            // APK questions enter as Inactive because they land in the 'uncategorized'
            // bucket. AssignCategoriesCommand promotes them to Active when keyword
            // matching successfully reassigns them to a real PDD category.
            // Anything still in 'uncategorized' after assign-categories stays Inactive
            // and is invisible to users until an admin reviews + categorizes it.
            var question = new Question
            {
                Id = Guid.NewGuid(),
                Text = new LocalizedText(uzText, uzLatin, ruText),
                Explanation = new LocalizedText(uzExpl, latExpl, ruExpl),
                Difficulty = difficulty,
                CategoryId = category.Id,
                ImageUrl = imageKey,
                ThumbnailUrl = thumbKey,
                LicenseCategory = LicenseCategory.AB,
                Status = QuestionStatus.Inactive,
                // Group into tickets: 20 questions each → ticket 1 = IDs 1-20, etc.
                TicketNumber = (id - 1) / 20 + 1,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Build AnswerOption entities — match by index across all 3 languages
            var minChoices = kirEntry.Choises.Count;
            if (latEntry is not null) minChoices = Math.Min(minChoices, latEntry.Choises.Count);
            minChoices = Math.Min(minChoices, ruEntry.Choises.Count);

            for (int i = 0; i < minChoices; i++)
            {
                var kirChoice = kirEntry.Choises[i];
                var latChoice = latEntry?.Choises.ElementAtOrDefault(i);
                var ruChoice = ruEntry.Choises[i];

                question.AnswerOptions.Add(new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    Text = new LocalizedText(
                        kirChoice.Text,
                        latChoice?.Text ?? UzbekTransliterator.ToLatin(kirChoice.Text),
                        ruChoice.Text),
                    IsCorrect = kirChoice.Answer,
                    SortOrder = i,
                    QuestionId = question.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            // Validate: exactly one correct answer must exist
            if (!question.AnswerOptions.Any(a => a.IsCorrect))
            {
                Console.WriteLine($"  [WARN] ID={id}: no correct answer found, skipping");
                localSkipped++;
                continue;
            }

            batch.Add(question);
            pendingDedupKeys.Add(dedupKey);

            // Flush batch
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
        Console.WriteLine($"  APK import done: {localImported} imported, {localSkipped} skipped, {localImages} images");
    }

    private static async Task<Category> GetOrCreateApkCategoryAsync(
        AppDbContext db,
        MigrationContext ctx,
        CancellationToken ct)
    {
        var slug = ctx.DefaultApkCategorySlug;
        var existing = await db.Categories
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);

        if (existing is not null)
            return existing;

        if (ctx.DryRun)
        {
            return new Category
            {
                Id = Guid.NewGuid(),
                Name = new LocalizedText("Таснифланмаган", "Tasniflanmagan", "Без категории"),
                Description = new LocalizedText(
                    "Мавзу тайинланмаган саволлар",
                    "Mavzu tayinlanmagan savollar",
                    "Вопросы без назначенной темы"),
                Slug = slug,
                IsActive = true,
                SortOrder = 999, // last in sort order
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Таснифланмаган", "Tasniflanmagan", "Без категории"),
            Description = new LocalizedText(
                "Мавзу тайинланмаган саволлар",
                "Mavzu tayinlanmagan savollar",
                "Вопросы без назначенной темы"),
            Slug = slug,
            IsActive = true,
            SortOrder = 999,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        Console.WriteLine($"  Created category '{slug}'");
        return category;
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
            Console.WriteLine($"  [WARN] Batch of {batch.Count} failed: {ex.Message}. Retrying individually...");
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

    private static List<ApkQuestion>? LoadJson(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  [INFO] File not found: {path}");
            return null;
        }
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
