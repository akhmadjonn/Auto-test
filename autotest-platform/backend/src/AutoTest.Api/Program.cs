using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AutoTest.Api.Middleware;
using AutoTest.Application;
using AutoTest.Application.Common.Models;
using AutoTest.Infrastructure;
using AutoTest.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var cliArgs = Environment.GetCommandLineArgs();
var isMigrationMode = cliArgs.Contains("--apply-migrations");
var isBackgroundMode = cliArgs.Contains("--background");

// --- MIGRATION MODE: apply EF Core migrations and exit ---
if (isMigrationMode)
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((_, config) => config.WriteTo.Console())
        .ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile("/settings.json", optional: true, reloadOnChange: false);
            config.AddJsonFile("/secrets.json", optional: true, reloadOnChange: false);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(context.Configuration.GetConnectionString("PostgreSQL"), npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "autotest");
                }).UseSnakeCaseNamingConvention());
        })
        .Build();

    Console.WriteLine("=== AutoTest Migrator ===");
    Console.WriteLine("Applying EF Core migrations...");

    try
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration failed: {ex}");
        Environment.Exit(1);
    }
}

// --- Shared: load additional config sources ---
var builder = WebApplication.CreateBuilder(args);

// Load mounted config files (Docker config + secret mounts)
builder.Configuration.AddJsonFile("/settings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile("/secrets.json", optional: true, reloadOnChange: false);

// Serilog
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Application & Infrastructure (background services only in background mode)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, registerBackgroundServices: isBackgroundMode);

// --- BACKGROUND MODE: hosted services only, no web API ---
if (isBackgroundMode)
{
    builder.Services.Configure<HostOptions>(options =>
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

    // Health check so Docker can verify the service is running
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "", name: "postgresql")
        .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

    var bgApp = builder.Build();

    // Seed database + warm cache (background workers need DB access)
    await InitializeAsync(bgApp);

    // Minimal health endpoint for Docker healthcheck
    bgApp.MapHealthChecks("/health");

    Console.WriteLine("=== AutoTest Background Service ===");
    Console.WriteLine("Running background workers (SessionExpiration, SubscriptionBilling, EskizTokenRefresh)...");

    await bgApp.RunAsync();
    return;
}

// --- API MODE: full web server, no background services ---

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// JWT Authentication
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "super-secret-key-for-development-only-min-32-chars";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "AutoTest",
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "AutoTest",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "", name: "postgresql")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Avtolider API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Rate Limiting — identity-based (CGNAT-safe: per-user, not per-IP)
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var userId = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        logger.LogWarning("Rate limit hit: {Path} user={UserId} ip={IP}", context.HttpContext.Request.Path, userId ?? "anon", ip);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail("RATE_LIMITED", "Too many requests. Try again later."), ct);
    };

    // Per-user policy for authenticated endpoints (60 req/min per user)
    var authPermitLimit = rateLimitConfig.GetValue("Authenticated:PermitLimit", 60);
    var authWindowSeconds = rateLimitConfig.GetValue("Authenticated:WindowSeconds", 60);
    options.AddPolicy("authenticated", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });

        // Fallback to IP for unauthenticated requests hitting authenticated endpoints
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"ip:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });

    // Anonymous policy for unauthenticated endpoints — generous per-IP (CGNAT-safe)
    var anonTokenLimit = rateLimitConfig.GetValue("Anonymous:TokenLimit", 200);
    var anonTokensPerMinute = rateLimitConfig.GetValue("Anonymous:TokensPerMinute", 200);
    options.AddPolicy("anonymous", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetTokenBucketLimiter($"ip:{ip}", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = anonTokenLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = anonTokensPerMinute
        });
    });
});

var app = builder.Build();

// Seed database + ensure MinIO bucket + warm cache
await InitializeAsync(app);

// Middleware pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").DisableRateLimiting();

Console.WriteLine("=== AutoTest API ===");
app.Run();

// --- Shared initialization: seed DB, ensure MinIO bucket, warm cache ---
static async Task InitializeAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<AutoTest.Infrastructure.Persistence.DbSeeder>();
        await seeder.SeedAsync();

        var minio = (AutoTest.Infrastructure.Services.MinioFileStorageService)
            scope.ServiceProvider.GetRequiredService<AutoTest.Application.Common.Interfaces.IFileStorageService>();
        await minio.EnsureBucketExistsAsync();

        var settingsService = app.Services.GetRequiredService<AutoTest.Application.Common.Interfaces.ISystemSettingsService>();
        await settingsService.ReloadFromDatabaseAsync();
    }
    catch (Exception ex)
    {
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        startupLogger.LogError(ex, "Startup seeding/init failed");
    }
}
