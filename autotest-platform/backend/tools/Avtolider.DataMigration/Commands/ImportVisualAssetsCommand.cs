using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Imports visual assets (road signs, markings, hazard labels, first aid, color vision) into MinIO.
/// These are extracted from Avto Test PRO APK and organized in data/visual_assets/.
/// Outputs a JSON manifest mapping original filenames → MinIO keys.
/// Color vision plates are uploaded with exact key names (not GUID-renamed).
/// </summary>
public static class ImportVisualAssetsCommand
{
    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║     IMPORT VISUAL ASSETS → MinIO     ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No uploads will happen.");
        Console.WriteLine();

        var assetsDir = Path.Combine(ctx.DataPath, "visual_assets");
        if (!Directory.Exists(assetsDir))
        {
            Console.WriteLine($"  [INFO] Visual assets directory not found: {assetsDir}");
            Console.WriteLine("  Run: python3 scripts/prepare_data.py (without --skip-visual)");
            Console.WriteLine("  Skipping visual assets import.");
            return;
        }

        if (!ctx.DryRun)
            await ctx.ImageSvc.EnsureBucketAsync(ct);

        var manifest = new Dictionary<string, string>();
        int totalUploaded = 0;

        // GUID-renamed uploads: signs/markings only (these are referenced by
        // RoadSign/RoadMarking rows that ImportRoadSignsCommand updates separately).
        // Hazard + first-aid use exact-key uploads + DB linking — see methods below.
        var categories = new[]
        {
            ("signs/axborot", "visual/signs/axborot"),
            ("signs/buyuruvchi", "visual/signs/buyuruvchi"),
            ("signs/imtiyozli", "visual/signs/imtiyozli"),
            ("signs/ogohlantiruvchi", "visual/signs/ogohlantiruvchi"),
            ("signs/qoshimcha", "visual/signs/qoshimcha"),
            ("signs/servis", "visual/signs/servis"),
            ("signs/taqiqlovchi", "visual/signs/taqiqlovchi"),
            ("markings/horizontal", "visual/markings/horizontal"),
            ("markings/vertical", "visual/markings/vertical"),
        };

        foreach (var (dirRelative, minioPrefix) in categories)
        {
            ct.ThrowIfCancellationRequested();

            var dir = Path.Combine(assetsDir, dirRelative);
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"  [SKIP] {dirRelative}: directory not found");
                continue;
            }

            var files = Directory.GetFiles(dir)
                .Where(f => IsImageFile(f))
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                Console.WriteLine($"  [SKIP] {dirRelative}: no image files");
                continue;
            }

            int uploaded = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                if (!ctx.DryRun)
                {
                    var result = await ctx.ImageSvc.UploadAsync(file, minioPrefix, ct);
                    if (result is not null)
                    {
                        manifest[$"{dirRelative}/{Path.GetFileName(file)}"] = result.Value.ImageKey;
                        uploaded++;
                        ctx.Stats.RecordImageUploaded();
                    }
                }
                else
                    uploaded++;
            }

            totalUploaded += uploaded;
            Console.WriteLine($"  {dirRelative}: {uploaded}/{files.Count} uploaded");
        }

        // Hazard labels & first-aid procedures — exact-key upload, then UPDATE
        // the corresponding entity row's ImageUrl/IconUrl. Random GUIDs would
        // create orphans on every reseed; exact keys + slug→filename mapping
        // keep DB and MinIO in sync.
        totalUploaded += await ImportHazardLabelsAsync(ctx, assetsDir, manifest, ct);
        totalUploaded += await ImportFirstAidProceduresAsync(ctx, assetsDir, manifest, ct);

        // Color vision plates — uploaded with exact key names (not GUID-renamed)
        // Backend expects keys: color-vision/plate-01.webp through plate-12.webp
        var colorVisionDir = Path.Combine(assetsDir, "color_vision");
        if (Directory.Exists(colorVisionDir))
        {
            var plateFiles = Directory.GetFiles(colorVisionDir)
                .Where(f => IsImageFile(f) && Path.GetFileNameWithoutExtension(f).StartsWith("plate-"))
                .OrderBy(f => f)
                .ToList();

            if (plateFiles.Count > 0)
            {
                int uploaded = 0;
                foreach (var file in plateFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    var plateName = Path.GetFileNameWithoutExtension(file);
                    var exactKey = $"color-vision/{plateName}.webp";

                    if (!ctx.DryRun)
                    {
                        var success = await ctx.ImageSvc.UploadExactKeyAsync(file, exactKey, ct);
                        if (success)
                        {
                            manifest[$"color_vision/{Path.GetFileName(file)}"] = exactKey;
                            uploaded++;
                            ctx.Stats.RecordImageUploaded();
                        }
                    }
                    else
                        uploaded++;
                }

                totalUploaded += uploaded;
                Console.WriteLine($"  color_vision: {uploaded}/{plateFiles.Count} uploaded (exact keys)");
            }
        }
        else
            Console.WriteLine("  [SKIP] color_vision: directory not found");

        // Save manifest
        if (manifest.Count > 0)
        {
            var manifestPath = Path.Combine(assetsDir, "manifest.json");
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(manifestPath, json, ct);
            Console.WriteLine($"\n  Manifest saved: {manifestPath} ({manifest.Count} entries)");
        }

        Console.WriteLine();
        Console.WriteLine($"  Visual assets import done: {totalUploaded} images uploaded");
    }

    // Maps the 18 DbSeeder hazard label slugs → the actual file basename in
    // data/visual_assets/hazard/. Source files are ADR/UN-style transport
    // hazard placards while DbSeeder uses GHS pictogram codes — best-effort
    // visual correspondence below. PO/admin can swap any pairing later
    // through the admin UI; this mapping is just the initial seed.
    private static readonly Dictionary<string, string> HazardSlugToFile = new()
    {
        ["explosive"]            = "1.1",                      // GHS01  — explosive (mass-explosion)
        ["self-reactive"]        = "1.4",                      // GHS01a — self-reactive (subdiv)
        ["organic-peroxide"]     = "organik_preroksidlar",     // GHS01b — organic peroxides
        ["flammable-gas"]        = "tez_alangalanuvchi",       // GHS02  — flammable gas
        ["flammable-aerosol"]    = "2-toifa",                  // GHS02a — flammable aerosols
        ["flammable-liquid"]     = "3-toifa",                  // GHS02b — ADR class 3 = flammable liquids
        ["flammable-solid"]      = "tez_modda",                // GHS02c — flammable solids
        ["pyrophoric"]           = "oz_ozidan",                // GHS02d — self-igniting
        ["self-heating"]         = "1.5",                      // GHS02e — self-heating (best available)
        ["water-reactive"]       = "suv_tekkanida",            // GHS02f — water-reactive
        ["oxidizing-gas"]        = "oksidlovchi",              // GHS03  — oxidizing gas
        ["oxidizer"]             = "1.6",                      // GHS03a — oxidizer (best available)
        ["gas-under-pressure"]   = "notaksik_gaz",             // GHS04  — gas cylinder / non-toxic gas
        ["corrosive"]            = "korroziyalanuvchi",        // GHS05  — corrosive
        ["toxic"]                = "toksik_moddalar",          // GHS06  — toxic
        ["irritant"]             = "boshqa",                   // GHS07  — irritant (placeholder)
        ["health-hazard"]        = "ikfeksiyali",              // GHS08  — health hazard
        ["environmental-hazard"] = "1_toifaliradiaktiv",       // GHS09  — environmental / radioactive
    };

    private static async Task<int> ImportHazardLabelsAsync(
        MigrationContext ctx,
        string assetsDir,
        Dictionary<string, string> manifest,
        CancellationToken ct)
    {
        var hazardDir = Path.Combine(assetsDir, "hazard");
        if (!Directory.Exists(hazardDir))
        {
            Console.WriteLine("  [SKIP] hazard: directory not found");
            return 0;
        }

        // Build filename (no extension) → full path lookup so the mapping above
        // doesn't have to know the actual extension on disk.
        var filesByStem = Directory.GetFiles(hazardDir)
            .Where(IsImageFile)
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), f => f);

        int uploaded = 0;
        var slugToKey = new Dictionary<string, string>(HazardSlugToFile.Count);

        foreach (var (slug, fileStem) in HazardSlugToFile)
        {
            ct.ThrowIfCancellationRequested();

            if (!filesByStem.TryGetValue(fileStem.ToLowerInvariant(), out var localPath))
            {
                Console.WriteLine($"  [WARN] hazard '{slug}': source file '{fileStem}.*' not found in data/visual_assets/hazard/");
                continue;
            }

            // Deterministic key — same path on every re-run, no orphans.
            var exactKey = $"visual/hazard/{fileStem}.webp";

            if (!ctx.DryRun)
            {
                var success = await ctx.ImageSvc.UploadExactKeyAsync(localPath, exactKey, ct);
                if (!success) continue;
                ctx.Stats.RecordImageUploaded();
            }

            slugToKey[slug] = exactKey;
            manifest[$"hazard/{Path.GetFileName(localPath)}"] = exactKey;
            uploaded++;
        }

        Console.WriteLine($"  hazard: {uploaded}/{HazardSlugToFile.Count} uploaded (exact keys, slug-mapped)");

        // Patch HazardLabel.ImageUrl so existing rows seeded with stale GUID URLs
        // (or empty URLs from a fresh seed) point to the actual MinIO keys.
        if (!ctx.DryRun && slugToKey.Count > 0)
        {
            var labels = await ctx.Db.HazardLabels.ToListAsync(ct);
            int updated = 0;
            foreach (var label in labels)
            {
                if (slugToKey.TryGetValue(label.Slug, out var key) && label.ImageUrl != key)
                {
                    label.ImageUrl = key;
                    updated++;
                }
            }
            if (updated > 0)
            {
                await ctx.Db.SaveChangesAsync(ct);
                ctx.Db.ChangeTracker.Clear();
            }
            Console.WriteLine($"  hazard: {updated} HazardLabel.ImageUrl rows updated to match");

            // Invalidate the API's Redis cache for hazard labels so the next
            // GET /hazard-labels request rebuilds from DB instead of serving
            // stale URLs from before this import. Cache key matches the one
            // GetHazardLabelsQueryHandler writes (50-min TTL otherwise).
            await InvalidateRedisKeyAsync(ctx.Config, "avtolider:hazard-labels:all", ct);
        }

        return uploaded;
    }

    // Maps the 8 DbSeeder first-aid procedure slugs → file basenames in
    // data/visual_assets/first_aid/. Source files use numbered driving-regulation
    // codes (48.x, 49.x, etc.); best-effort visual correspondence below.
    // PO/admin can swap any pairing later through the admin UI.
    private static readonly Dictionary<string, string> FirstAidSlugToFile = new()
    {
        ["cpr"]              = "56.1",   // Сердечно-лёгочная реанимация
        ["wound-treatment"]  = "49.1",   // Обработка ран
        ["fracture-care"]    = "54.1",   // Помощь при переломах
        ["burns"]            = "58.1",   // Помощь при ожогах
        ["severe-bleeding"]  = "48.1",   // Сильное кровотечение
        ["choking"]          = "57.1",   // Удушье
        ["shock"]            = "48.2",   // Шоковое состояние
        ["poisoning"]        = "58.2",   // Отравление
    };

    private static async Task<int> ImportFirstAidProceduresAsync(
        MigrationContext ctx,
        string assetsDir,
        Dictionary<string, string> manifest,
        CancellationToken ct)
    {
        var firstAidDir = Path.Combine(assetsDir, "first_aid");
        if (!Directory.Exists(firstAidDir))
        {
            Console.WriteLine("  [SKIP] first_aid: directory not found");
            return 0;
        }

        var filesByStem = Directory.GetFiles(firstAidDir)
            .Where(IsImageFile)
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant(), f => f);

        int uploaded = 0;
        var slugToKey = new Dictionary<string, string>(FirstAidSlugToFile.Count);

        foreach (var (slug, fileStem) in FirstAidSlugToFile)
        {
            ct.ThrowIfCancellationRequested();

            if (!filesByStem.TryGetValue(fileStem.ToLowerInvariant(), out var localPath))
            {
                Console.WriteLine($"  [WARN] first-aid '{slug}': source file '{fileStem}.*' not found");
                continue;
            }

            var exactKey = $"visual/first_aid/{fileStem}.webp";

            if (!ctx.DryRun)
            {
                var success = await ctx.ImageSvc.UploadExactKeyAsync(localPath, exactKey, ct);
                if (!success) continue;
                ctx.Stats.RecordImageUploaded();
            }

            slugToKey[slug] = exactKey;
            manifest[$"first_aid/{Path.GetFileName(localPath)}"] = exactKey;
            uploaded++;
        }

        Console.WriteLine($"  first_aid: {uploaded}/{FirstAidSlugToFile.Count} uploaded (exact keys, slug-mapped)");

        if (!ctx.DryRun && slugToKey.Count > 0)
        {
            var procedures = await ctx.Db.FirstAidProcedures.ToListAsync(ct);
            int updated = 0;
            foreach (var proc in procedures)
            {
                if (slugToKey.TryGetValue(proc.Slug, out var key) && proc.IconUrl != key)
                {
                    proc.IconUrl = key;
                    updated++;
                }
            }
            if (updated > 0)
            {
                await ctx.Db.SaveChangesAsync(ct);
                ctx.Db.ChangeTracker.Clear();
            }
            Console.WriteLine($"  first_aid: {updated} FirstAidProcedure.IconUrl rows updated to match");

            await InvalidateRedisKeyAsync(ctx.Config, "avtolider:first-aid:all", ct);
        }

        return uploaded;
    }

    private static async Task InvalidateRedisKeyAsync(IConfiguration config, string key, CancellationToken ct)
    {
        var conn = config.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Console.WriteLine($"  [INFO] No Redis connection configured; skipping cache invalidation for '{key}'");
            return;
        }

        try
        {
            using var redis = await ConnectionMultiplexer.ConnectAsync(conn);
            var deleted = await redis.GetDatabase().KeyDeleteAsync(key);
            Console.WriteLine($"  Redis cache key '{key}' {(deleted ? "invalidated" : "was not present")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARN] Failed to invalidate Redis key '{key}': {ex.Message}");
        }
    }

    private static bool IsImageFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
}
