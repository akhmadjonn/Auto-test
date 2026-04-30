using Amazon.S3;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Infrastructure.Persistence;
using AutoTest.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AutoTest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool registerBackgroundServices = false)
    {
        // PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL"), npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "autotest");
                }));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DbSeeder>();

        // Redis
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // MinIO (S3-compatible) — two clients:
        //   • IAmazonS3            → INTERNAL endpoint (e.g. minio:9000) — uploads, bucket ops
        //   • MinioPresignClient   → PUBLIC endpoint (e.g. cdn.avtolider.uz) — browser-facing presigned URLs
        // Splitting these avoids the Docker hairpin NAT issue: a container connecting
        // to its own host's public IP via Docker bridge often gets ECONNREFUSED.
        var minioEndpoint = configuration["MinioSettings:Endpoint"] ?? "localhost:9000";
        var minioAccessKey = configuration["MinioSettings:AccessKey"] ?? "minioadmin";
        var minioSecretKey = configuration["MinioSettings:SecretKey"] ?? "minioadmin";
        var useSSL = bool.TryParse(configuration["MinioSettings:UseSSL"], out var ssl) && ssl;

        // Public endpoint falls back to the internal one if not configured.
        var publicEndpoint = configuration["MinioSettings:PublicEndpoint"] ?? minioEndpoint;
        var publicUseSSL = bool.TryParse(configuration["MinioSettings:PublicUseSSL"], out var pssl)
            ? pssl
            : useSSL;

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            minioAccessKey,
            minioSecretKey,
            new AmazonS3Config
            {
                ServiceURL = $"{(useSSL ? "https" : "http")}://{minioEndpoint}",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = !useSSL
            }));

        services.AddSingleton(_ => new MinioPresignClient(new AmazonS3Client(
            minioAccessKey,
            minioSecretKey,
            new AmazonS3Config
            {
                ServiceURL = $"{(publicUseSSL ? "https" : "http")}://{publicEndpoint}",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = !publicUseSSL
            })));

        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // Eskiz SMS — trailing slash required for HttpClient relative URI resolution
        var eskizBaseUrl = configuration["EskizSettings:BaseUrl"] ?? "https://notify.eskiz.uz/api/";
        if (!eskizBaseUrl.EndsWith('/')) eskizBaseUrl += '/';
        services.AddHttpClient("Eskiz", client =>
        {
            client.BaseAddress = new Uri(eskizBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ISmsService, EskizSmsService>();

        // Auth services
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<ITelegramAuthService, TelegramAuthService>();

        // Processing services
        services.AddScoped<IImageProcessingService, QuestionImageProcessor>();
        services.AddScoped<IQuestionImportService, ExcelQuestionParser>();
        services.AddScoped<IExcelExportService, ExcelExportService>();

        // Admin services
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();

        // Core services
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddHttpContextAccessor();

        // Practice / Spaced Repetition
        services.AddScoped<IPracticeService, LeitnerBoxService>();

        // Engagement / XP
        services.AddScoped<IXpService, XpService>();

        // Telegram notifications
        services.AddHttpClient("Telegram");
        services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();

        // Transliteration
        services.AddSingleton<ITransliterationService, UzbekTransliterator>();

        // Payment providers
        services.AddHttpClient("Payme", client =>
        {
            client.BaseAddress = new Uri(configuration["PaymeSettings:BaseUrl"] ?? "https://checkout.paycom.uz/api");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient("Click", client =>
        {
            client.BaseAddress = new Uri(configuration["ClickSettings:BaseUrl"] ?? "https://api.click.uz/v2/merchant");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<PaymePaymentProvider>();
        services.AddScoped<ClickPaymentProvider>();
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        // Background services — only registered in background mode
        if (registerBackgroundServices)
        {
            services.AddHostedService<SessionExpirationService>();
            services.AddHostedService<SubscriptionBillingService>();
            services.AddHostedService<EskizTokenRefreshService>();
            services.AddHostedService<DataCleanupService>();
            services.AddHostedService<LeaderboardSnapshotService>();
            services.AddHostedService<StudyReminderService>();
        }

        return services;
    }
}
