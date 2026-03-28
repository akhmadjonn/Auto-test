using Avtolider.DataMigration.Services;
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

        // Process each asset category (GUID-renamed uploads)
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
            ("hazard", "visual/hazard"),
            ("first_aid", "visual/first_aid"),
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

    private static bool IsImageFile(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
}
