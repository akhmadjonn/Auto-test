using AutoTest.Api;
using AutoTest.Application;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Services;
using AutoTest.Infrastructure;
using AutoTest.Infrastructure.Persistence;
using AutoTest.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var cliArgs = Environment.GetCommandLineArgs();
var isMigrationMode = cliArgs.Contains("--apply-migrations");
var isBackgroundMode = cliArgs.Contains("--background");

// --- MIGRATION MODE: apply EF Core migrations, seed data, and exit ---
if (isMigrationMode)
{
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
           ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
           ?? "Production";

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((_, config) => config.WriteTo.Console())
        .ConfigureAppConfiguration((_, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.json", optional: false);
            config.AddJsonFile($"appsettings.{env}.json", optional: true);
            config.AddJsonFile("/settings.json", optional: true, reloadOnChange: false);
            config.AddJsonFile("/secrets.json", optional: true, reloadOnChange: false);
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddInfrastructure(context.Configuration);
            services.AddApplication();
        })
        .Build();

    Console.WriteLine("=== AutoTest Migrator ===");

    try
    {
        using var scope = host.Services.CreateScope();

        Console.WriteLine("Applying EF Core migrations...");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");

        Console.WriteLine("Seeding database...");
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
        Console.WriteLine("Database seeding completed.");

        Console.WriteLine("Ensuring MinIO bucket...");
        var minio = (MinioFileStorageService)scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await minio.EnsureBucketExistsAsync();
        Console.WriteLine("MinIO bucket ready.");

        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration/seeding failed: {ex}");
        Environment.Exit(1);
    }
}

// --- Shared builder setup ---
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("/settings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile("/secrets.json", optional: true, reloadOnChange: false);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, registerBackgroundServices: isBackgroundMode);

// --- BACKGROUND MODE: hosted services only, no web API ---
if (isBackgroundMode)
{
    builder.Services.Configure<HostOptions>(options =>
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "", name: "postgresql")
        .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

    var bgApp = builder.Build();

    var settingsService = bgApp.Services.GetRequiredService<ISystemSettingsService>();
    await settingsService.ReloadFromDatabaseAsync();

    bgApp.MapHealthChecks("/health");

    Console.WriteLine("=== AutoTest Background Service ===");
    await bgApp.RunAsync();
    return;
}

// --- API MODE ---
builder.AddApiServices();

var app = builder.Build();

var apiSettingsService = app.Services.GetRequiredService<ISystemSettingsService>();
await apiSettingsService.ReloadFromDatabaseAsync();

// Load localized error messages from CSV (Resources/localizations.csv is copied to output)
var localizationService = app.Services.GetRequiredService<LocalizationService>();
localizationService.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Resources", "localizations.csv"));

app.ConfigureMiddleware();

Console.WriteLine("=== AutoTest API ===");
app.Run();
