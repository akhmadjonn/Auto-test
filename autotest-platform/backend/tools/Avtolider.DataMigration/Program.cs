using Amazon.S3;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Commands;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// ── Parse CLI arguments ───────────────────────────────────────────────────────
var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
var dryRun = args.Contains("--dry-run");
var hardDelete = args.Contains("--hard");

if (command == "help" || command == "--help" || command == "-h")
{
    PrintUsage();
    return 0;
}

// ── Load configuration ────────────────────────────────────────────────────────
var appDir = AppContext.BaseDirectory;
var config = new ConfigurationBuilder()
    .SetBasePath(appDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables("MIGRATION_")
    .Build();

// ── Build DbContext ───────────────────────────────────────────────────────────
var connStr = config.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL is required in appsettings.json");

var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connStr, npgsql =>
    {
        npgsql.MigrationsHistoryTable("__ef_migrations_history", "autotest");
    })
    .UseSnakeCaseNamingConvention()
    .LogTo(_ => { }, Microsoft.Extensions.Logging.LogLevel.None) // suppress EF Core noise
    .Options;

// ── Build MinIO S3 client ─────────────────────────────────────────────────────
var minioEndpoint = config["MinioSettings:Endpoint"] ?? "localhost:9000";
var minioAccessKey = config["MinioSettings:AccessKey"] ?? "minioadmin";
var minioSecretKey = config["MinioSettings:SecretKey"] ?? "minioadmin";
var useSSL = bool.TryParse(config["MinioSettings:UseSSL"], out var ssl) && ssl;
var bucket = config["MinioSettings:BucketName"] ?? "autotest-images";

var s3 = new AmazonS3Client(
    minioAccessKey,
    minioSecretKey,
    new AmazonS3Config
    {
        ServiceURL = $"{(useSSL ? "https" : "http")}://{minioEndpoint}",
        ForcePathStyle = true,
        AuthenticationRegion = "us-east-1"
    });

// ── Build services ────────────────────────────────────────────────────────────
var imageSvc = new ImageMigrationService(s3, bucket);
var stats = new MigrationStats();

// Resolve data path relative to the tool's working directory (not BaseDirectory)
// so that `dotnet run -- import-apk` finds data/ next to the .csproj
var dataPathFromConfig = config["MigrationSettings:DataPath"] ?? "data";
var resolvedDataPath = Path.IsPathRooted(dataPathFromConfig)
    ? dataPathFromConfig
    : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, dataPathFromConfig));

// Override config DataPath with resolved absolute path
var configWithDataPath = new ConfigurationBuilder()
    .AddConfiguration(config)
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["MigrationSettings:DataPath"] = resolvedDataPath
    })
    .Build();

// ── Wire CancellationToken ────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine("\n  [INFO] Cancellation requested, finishing current batch...");
    e.Cancel = true; // prevent process kill; let cts propagate
    cts.Cancel();
};

// ── Execute command ───────────────────────────────────────────────────────────
Console.WriteLine($"Avtolider Data Migration Tool");
Console.WriteLine($"Command: {command}{(dryRun ? " [DRY RUN]" : "")}");
Console.WriteLine($"Data path: {resolvedDataPath}");
Console.WriteLine($"Database: {MaskConnectionString(connStr)}");
Console.WriteLine();

try
{
    await using var db = new AppDbContext(dbOptions);

    // Verify DB connectivity
    Console.Write("Checking database connection... ");
    await db.Database.OpenConnectionAsync(cts.Token);
    await db.Database.CloseConnectionAsync();
    Console.WriteLine("OK");

    var ctx = new MigrationContext(db, imageSvc, configWithDataPath, stats, dryRun);

    switch (command)
    {
        case "import-apk":
            await ImportApkCommand.ExecuteAsync(ctx, cts.Token);
            break;

        case "import-avtolider":
            await ImportAvtoliderCommand.ExecuteAsync(ctx, cts.Token);
            break;

        case "deduplicate":
            await DeduplicateCommand.ExecuteAsync(ctx, hardDelete, cts.Token);
            break;

        case "import-all":
            Console.WriteLine("Running all imports in sequence...");
            await ImportApkCommand.ExecuteAsync(ctx, cts.Token);
            await ImportAvtoliderCommand.ExecuteAsync(ctx, cts.Token);
            await DeduplicateCommand.ExecuteAsync(ctx, hardDelete, cts.Token);
            break;

        default:
            Console.WriteLine($"  [ERROR] Unknown command: '{command}'");
            PrintUsage();
            return 1;
    }

    stats.PrintSummary();
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n  [INFO] Operation cancelled by user.");
    stats.PrintSummary();
    return 130; // SIGINT convention
}
catch (Exception ex)
{
    Console.WriteLine($"\n  [FATAL] {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null)
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
    Console.WriteLine();
    Console.WriteLine("Tip: Check your appsettings.json connection strings and make sure PostgreSQL and MinIO are running.");
    return 1;
}

// ── Helpers ───────────────────────────────────────────────────────────────────
static void PrintUsage()
{
    Console.WriteLine();
    Console.WriteLine("  Usage:");
    Console.WriteLine("    dotnet run -- <command> [options]");
    Console.WriteLine();
    Console.WriteLine("  Commands:");
    Console.WriteLine("    import-apk         Import 700 questions from APK JSON files (uzkiril/uzlotin/rus)");
    Console.WriteLine("    import-avtolider   Import questions from Avtolider DB JSON export");
    Console.WriteLine("    deduplicate        Fuzzy-match and remove duplicate questions (Levenshtein <20%)");
    Console.WriteLine("    import-all         Run all imports then deduplicate in sequence");
    Console.WriteLine();
    Console.WriteLine("  Options:");
    Console.WriteLine("    --dry-run          Preview what would be imported/deleted without writing");
    Console.WriteLine("    --hard             (deduplicate only) Hard-delete duplicates instead of soft-delete");
    Console.WriteLine();
    Console.WriteLine("  Examples:");
    Console.WriteLine("    dotnet run -- import-apk");
    Console.WriteLine("    dotnet run -- import-all --dry-run");
    Console.WriteLine("    dotnet run -- deduplicate --hard");
    Console.WriteLine();
    Console.WriteLine("  Config: Edit appsettings.json or appsettings.local.json for connection strings.");
    Console.WriteLine("  Env override prefix: MIGRATION_ (e.g. MIGRATION_ConnectionStrings__PostgreSQL=...)");
    Console.WriteLine();
}

static string MaskConnectionString(string cs)
{
    // Mask password in connection string for safe logging
    var parts = cs.Split(';');
    return string.Join(';', parts.Select(p =>
    {
        var kv = p.Split('=', 2);
        return kv.Length == 2 && kv[0].Trim().Equals("Password", StringComparison.OrdinalIgnoreCase)
            ? $"{kv[0]}=***"
            : p;
    }));
}
