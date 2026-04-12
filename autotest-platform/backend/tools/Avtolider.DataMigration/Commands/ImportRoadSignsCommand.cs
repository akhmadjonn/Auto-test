using AutoTest.Domain.Common.Enums;
using Avtolider.DataMigration.Services;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Imports road sign and road marking images from extracted competitor APKs into MinIO,
/// and updates the corresponding RoadSign/RoadMarking records with ImageUrl/ThumbnailUrl.
///
/// Image sources (tried in priority order):
///   1. Pravachi: _extracted/Pravachi/.../signs/{category}/{code}.webp (310 files, WebP, organized by category)
///   2. AvtoTestPro: _extracted/AvtoTestPro/.../belgilar/{category}/{code}.webp (327 files, WebP)
///
/// The sign code in the filename (e.g., "1.1.webp" → code "1.1") is matched against
/// RoadSign.SignCode / RoadMarking.MarkingCode in the database.
///
/// Also creates new RoadSign records for images found in competitor apps but missing from our seeded data.
/// </summary>
public static class ImportRoadSignsCommand
{
    // Pravachi folder → our DB category code mapping
    // File names inside Pravachi folders already contain the standard sign code (1.1, 2.3.1, etc.)
    private static readonly (string FolderName, string CategoryCode)[] PravachiCategories =
    [
        ("warning", "1"),
        ("priority", "2"),
        ("prohibitory", "3"),
        ("mandatory", "4"),
        ("service", "6"),        // Pravachi "service" folder has 5.x codes → but in UZ PDD those are service (cat 6)
        ("informational", "5"),  // Pravachi "informational" folder has 6.x codes → but in UZ PDD those are info (cat 5)
        ("additional", "7"),
    ];

    // AvtoTestPro folder → our DB category code mapping
    private static readonly (string FolderName, string CategoryCode)[] AvtoTestProCategories =
    [
        ("ogohlantiruvchi", "1"),
        ("imtiyozli", "2"),
        ("taqiqlovchi", "3"),
        ("buyuruvchi", "4"),
        ("axborot", "5"),
        ("servis", "6"),
        ("qoshimcha", "7"),
    ];

    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║  IMPORT ROAD SIGN & MARKING IMAGES       ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No uploads or DB changes.");
        Console.WriteLine();

        if (!ctx.DryRun)
            await ctx.ImageSvc.EnsureBucketAsync(ct);

        // Load all road sign categories from DB
        var dbCategories = await ctx.Db.RoadSignCategories
            .AsNoTracking()
            .Select(c => new { c.Id, c.Code })
            .ToListAsync(ct);

        var categoryMap = dbCategories.ToDictionary(c => c.Code, c => c.Id);
        Console.WriteLine($"  Found {dbCategories.Count} road sign categories in DB");

        // Load all existing road signs from DB
        var dbSigns = await ctx.Db.RoadSigns.ToListAsync(ct);
        var signByCode = dbSigns.ToDictionary(s => s.SignCode, s => s);
        Console.WriteLine($"  Found {dbSigns.Count} road signs in DB");

        // Load all existing road markings from DB
        var dbMarkings = await ctx.Db.RoadMarkings.ToListAsync(ct);
        var markingByCode = dbMarkings.ToDictionary(m => m.MarkingCode, m => m);
        Console.WriteLine($"  Found {dbMarkings.Count} road markings in DB");
        Console.WriteLine();

        // ── Build image lookup: signCode → filePath ──────────────────────
        var signImageMap = new Dictionary<string, string>(); // code → filePath

        // Try Pravachi first (better quality WebP images)
        var pravachiBase = FindPravachiSignsDir(ctx);
        if (pravachiBase is not null)
        {
            Console.WriteLine($"  [SOURCE] Pravachi signs: {pravachiBase}");
            foreach (var (folder, _) in PravachiCategories)
            {
                var dir = Path.Combine(pravachiBase, folder);
                if (!Directory.Exists(dir))
                    continue;

                foreach (var file in Directory.GetFiles(dir).Where(IsImageFile))
                {
                    var code = Path.GetFileNameWithoutExtension(file); // "1.1" from "1.1.webp"
                    if (code == "asosiy") continue; // skip category cover images
                    signImageMap.TryAdd(code, file);
                }
            }
            Console.WriteLine($"  Pravachi: {signImageMap.Count} sign images found");
        }

        // Fill gaps from AvtoTestPro
        var avtoTestProBase = FindAvtoTestProSignsDir(ctx);
        if (avtoTestProBase is not null)
        {
            int addedFromPro = 0;
            Console.WriteLine($"  [SOURCE] AvtoTestPro signs: {avtoTestProBase}");
            foreach (var (folder, _) in AvtoTestProCategories)
            {
                var dir = Path.Combine(avtoTestProBase, folder);
                if (!Directory.Exists(dir))
                    continue;

                foreach (var file in Directory.GetFiles(dir).Where(IsImageFile))
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    if (code == "asosiy") continue;
                    if (signImageMap.TryAdd(code, file))
                        addedFromPro++;
                }
            }
            Console.WriteLine($"  AvtoTestPro: +{addedFromPro} new codes (total: {signImageMap.Count})");
        }

        if (signImageMap.Count == 0)
        {
            Console.WriteLine("  [ERROR] No sign images found. Make sure competitor APKs are extracted.");
            Console.WriteLine("  Expected at:");
            Console.WriteLine("    ../Competitor's apps/_extracted/Pravachi/.../signs/");
            Console.WriteLine("    ../Competitor's apps/_extracted/AvtoTestPro/.../belgilar/");
            return;
        }

        Console.WriteLine();

        // ── Upload images for existing signs ─────────────────────────────
        int uploaded = 0, skippedHasImage = 0, newSignsCreated = 0;

        foreach (var (code, filePath) in signImageMap.OrderBy(kv => kv.Key))
        {
            ct.ThrowIfCancellationRequested();

            if (signByCode.TryGetValue(code, out var sign))
            {
                // Sign exists in DB — upload image if missing
                if (!string.IsNullOrEmpty(sign.ImageUrl))
                {
                    skippedHasImage++;
                    continue;
                }

                if (!ctx.DryRun)
                {
                    var result = await ctx.ImageSvc.UploadAsync(filePath, "road-signs", ct);
                    if (result is not null)
                    {
                        sign.ImageUrl = result.Value.ImageKey;
                        sign.ThumbnailUrl = result.Value.ThumbKey;
                        sign.UpdatedAt = DateTimeOffset.UtcNow;
                        uploaded++;
                        ctx.Stats.RecordImageUploaded();
                    }
                }
                else
                    uploaded++;
            }
            else
            {
                // Sign NOT in DB — create new record + upload image
                var catCode = InferCategoryCode(code);
                if (!categoryMap.TryGetValue(catCode, out var categoryId))
                {
                    Console.WriteLine($"    [SKIP] {code}: no category for code prefix '{catCode}'");
                    continue;
                }

                if (!ctx.DryRun)
                {
                    var result = await ctx.ImageSvc.UploadAsync(filePath, "road-signs", ct);
                    var newSign = new AutoTest.Domain.Entities.RoadSign
                    {
                        Id = Guid.NewGuid(),
                        CategoryId = categoryId,
                        SignCode = code,
                        Name = new AutoTest.Domain.Common.ValueObjects.LocalizedText(
                            $"Белги {code}", $"Belgi {code}", $"Знак {code}"),
                        ImageUrl = result?.ImageKey ?? "",
                        ThumbnailUrl = result?.ThumbKey,
                        SortOrder = ParseSortOrder(code),
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    ctx.Db.RoadSigns.Add(newSign);
                    signByCode[code] = newSign;

                    if (result is not null)
                        ctx.Stats.RecordImageUploaded();
                }

                newSignsCreated++;
                ctx.Stats.RecordImported();
            }
        }

        // ── Upload category icons (asosiy.webp files) ────────────────────
        int categoryIcons = 0;
        if (pravachiBase is not null)
        {
            foreach (var (folder, catCode) in PravachiCategories)
            {
                ct.ThrowIfCancellationRequested();

                var iconFile = Path.Combine(pravachiBase, folder, "asosiy.webp");
                if (!File.Exists(iconFile))
                    continue;

                if (!categoryMap.TryGetValue(catCode, out var catId))
                    continue;

                var category = await ctx.Db.RoadSignCategories.FindAsync([catId], ct);
                if (category is null || !string.IsNullOrEmpty(category.IconUrl))
                    continue;

                if (!ctx.DryRun)
                {
                    var result = await ctx.ImageSvc.UploadAsync(iconFile, "road-sign-categories", ct);
                    if (result is not null)
                    {
                        category.IconUrl = result.Value.ImageKey;
                        category.UpdatedAt = DateTimeOffset.UtcNow;
                        categoryIcons++;
                        ctx.Stats.RecordImageUploaded();
                    }
                }
                else
                    categoryIcons++;
            }
        }

        // ── Upload road marking images (from AvtoTestPro chiziq/yotiq + chiziq/tik) ──
        int markingUploaded = 0;
        var markingBaseDir = FindRoadMarkingsDir(ctx);
        if (markingBaseDir is not null)
        {
            Console.WriteLine($"  [SOURCE] Road markings: {markingBaseDir}");

            // Scan both subdirectories: yotiq (horizontal) and tik (vertical)
            var markingSubDirs = new[] { "yotiq", "tik" };
            foreach (var subDir in markingSubDirs)
            {
                var dir = Path.Combine(markingBaseDir, subDir);
                if (!Directory.Exists(dir))
                {
                    // Also try flat directory (files directly in chiziq/)
                    dir = markingBaseDir;
                }

                foreach (var file in Directory.GetFiles(dir).Where(IsImageFile).OrderBy(f => f))
                {
                    ct.ThrowIfCancellationRequested();

                    var rawName = Path.GetFileNameWithoutExtension(file); // "1.14.2_A" or "2.1_B" or "1.1"
                    if (rawName == "umumiy" || rawName == "asosiy") continue; // skip cover images

                    // Strip variant suffixes: "2.1_A" → "2.1", "1.14.2_A" → "1.14.2"
                    var underscoreIdx = rawName.IndexOf('_');
                    var code = underscoreIdx > 0 ? rawName[..underscoreIdx] : rawName;

                    if (!markingByCode.TryGetValue(code, out var marking))
                        continue;

                    if (!string.IsNullOrEmpty(marking.ImageUrl))
                        continue;

                    if (!ctx.DryRun)
                    {
                        var result = await ctx.ImageSvc.UploadAsync(file, "road-markings", ct);
                        if (result is not null)
                        {
                            marking.ImageUrl = result.Value.ImageKey;
                            marking.ThumbnailUrl = result.Value.ThumbKey;
                            marking.UpdatedAt = DateTimeOffset.UtcNow;
                            markingUploaded++;
                            ctx.Stats.RecordImageUploaded();
                        }
                    }
                    else
                        markingUploaded++;
                }

                // Don't scan flat dir twice if we already scanned subdirs
                if (dir == markingBaseDir) break;
            }
        }

        // ── Save all changes ─────────────────────────────────────────────
        if (!ctx.DryRun)
            await ctx.Db.SaveChangesAsync(ct);

        Console.WriteLine();
        Console.WriteLine("  ── Road Signs Summary ──");
        Console.WriteLine($"  Images uploaded to existing signs: {uploaded}");
        Console.WriteLine($"  Skipped (already has image):       {skippedHasImage}");
        Console.WriteLine($"  New signs created from images:     {newSignsCreated}");
        Console.WriteLine($"  Category icons uploaded:           {categoryIcons}");
        Console.WriteLine($"  Road marking images uploaded:      {markingUploaded}");
        Console.WriteLine($"  Total images in source:            {signImageMap.Count}");
    }

    /// <summary>
    /// Infer the category code from a sign code.
    /// "1.1" → "1", "2.3.1" → "2", "5.8.2" → "5", "7.1.1" → "7"
    /// </summary>
    private static string InferCategoryCode(string signCode)
    {
        var dotIndex = signCode.IndexOf('.');
        return dotIndex > 0 ? signCode[..dotIndex] : signCode;
    }

    /// <summary>
    /// Parse a numeric sort order from sign code for natural ordering.
    /// "1.1" → 1001, "1.12" → 1012, "2.3.1" → 2031, "5.8.2" → 5082
    /// </summary>
    private static int ParseSortOrder(string code)
    {
        var parts = code.Split('.');
        int order = 0;
        for (int i = 0; i < parts.Length && i < 4; i++)
        {
            if (int.TryParse(parts[i], out var num))
                order = order * 100 + num;
        }
        return order;
    }

    private static string? FindPravachiSignsDir(MigrationContext ctx)
    {
        // Try relative to project root (common setup)
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(ctx.DataPath, "..", "..", "..", "..", "Competitor's apps", "_extracted", "Pravachi",
                "assets", "composeResources", "avtotestmobile.composeapp.generated.resources", "files", "img", "signs")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "Competitor's apps", "_extracted", "Pravachi",
                "assets", "composeResources", "avtotestmobile.composeapp.generated.resources", "files", "img", "signs")),
            @"C:\Users\ASirozhiddinov\Documents\Claude projects\Auto test\Competitor's apps\_extracted\Pravachi\assets\composeResources\avtotestmobile.composeapp.generated.resources\files\img\signs",
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindAvtoTestProSignsDir(MigrationContext ctx)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(ctx.DataPath, "..", "..", "..", "..", "Competitor's apps", "_extracted", "AvtoTestPro",
                "assets", "flutter_assets", "assets", "images", "belgilar")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "Competitor's apps", "_extracted", "AvtoTestPro",
                "assets", "flutter_assets", "assets", "images", "belgilar")),
            @"C:\Users\ASirozhiddinov\Documents\Claude projects\Auto test\Competitor's apps\_extracted\AvtoTestPro\assets\flutter_assets\assets\images\belgilar",
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string? FindRoadMarkingsDir(MigrationContext ctx)
    {
        // AvtoTestPro has road markings in a "chiziq" folder
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(ctx.DataPath, "..", "..", "..", "..", "Competitor's apps", "_extracted", "AvtoTestPro",
                "assets", "flutter_assets", "assets", "images", "chiziq")),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "Competitor's apps", "_extracted", "AvtoTestPro",
                "assets", "flutter_assets", "assets", "images", "chiziq")),
            @"C:\Users\ASirozhiddinov\Documents\Claude projects\Auto test\Competitor's apps\_extracted\AvtoTestPro\assets\flutter_assets\assets\images\chiziq",
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static bool IsImageFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
}
