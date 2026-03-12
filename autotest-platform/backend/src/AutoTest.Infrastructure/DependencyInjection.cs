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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // PostgreSQL
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL"), npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "autotest");
                })
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DbSeeder>();

        // Redis
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        // MinIO (S3-compatible)
        var minioEndpoint = configuration["MinioSettings:Endpoint"] ?? "localhost:9000";
        var minioAccessKey = configuration["MinioSettings:AccessKey"] ?? "minioadmin";
        var minioSecretKey = configuration["MinioSettings:SecretKey"] ?? "minioadmin";
        var useSSL = bool.TryParse(configuration["MinioSettings:UseSSL"], out var ssl) && ssl;

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            minioAccessKey,
            minioSecretKey,
            new AmazonS3Config
            {
                ServiceURL = $"{(useSSL ? "https" : "http")}://{minioEndpoint}",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            }));
        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        // Eskiz SMS
        services.AddHttpClient("Eskiz", client =>
        {
            client.BaseAddress = new Uri(configuration["EskizSettings:BaseUrl"] ?? "https://notify.eskiz.uz/api/");
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

        // Background services
        services.AddHostedService<SessionExpirationService>();
        services.AddHostedService<SubscriptionBillingService>();
        services.AddHostedService<EskizTokenRefreshService>();

        return services;
    }
}
