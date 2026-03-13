using AutoTest.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Avtolider.DataMigration.Services;

/// <summary>
/// Shared state passed to all migration commands.
/// Encapsulates DB, S3, configuration, stats, and runtime flags.
/// </summary>
public sealed class MigrationContext(
    AppDbContext db,
    ImageMigrationService imageSvc,
    IConfiguration config,
    MigrationStats stats,
    bool dryRun)
{
    public AppDbContext Db { get; } = db;
    public ImageMigrationService ImageSvc { get; } = imageSvc;
    public IConfiguration Config { get; } = config;
    public MigrationStats Stats { get; } = stats;
    public bool DryRun { get; } = dryRun;

    public string DataPath =>
        Config["MigrationSettings:DataPath"] ?? "data";

    public int BatchSize =>
        int.TryParse(Config["MigrationSettings:BatchSize"], out var b) ? b : 50;

    public string DefaultApkCategorySlug =>
        Config["MigrationSettings:DefaultApkCategorySlug"] ?? "apk-savollari";

    public string DefaultApkCategoryName =>
        Config["MigrationSettings:DefaultApkCategoryName"] ?? "APK Savollari";
}
