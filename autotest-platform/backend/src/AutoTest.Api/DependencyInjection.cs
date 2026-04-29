using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AutoTest.Api.Services;
using AutoTest.Application.Common.Models;
using AutoTest.Application.Common.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace AutoTest.Api;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.AddJsonSerialization();
        builder.AddJwtAuthentication();
        builder.Services.AddAuthorization();
        builder.AddCorsPolicy();
        builder.AddHealthCheckServices();
        builder.AddSwaggerServices();
        builder.AddRateLimitingPolicies();
        builder.AddLocalizedResponseServices();

        return builder;
    }

    private static void AddLocalizedResponseServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddScoped<IResponseService, ResponseService>();
    }

    private static void AddJsonSerialization(this WebApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options => ConfigureJsonOptions(options.SerializerOptions));
        builder.Services.AddControllers().AddJsonOptions(options => ConfigureJsonOptions(options.JsonSerializerOptions));

        static void ConfigureJsonOptions(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }
    }

    private static void AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
            ?? "super-secret-key-for-development-only-min-32-chars";

        if (!builder.Environment.IsDevelopment() && jwtKey == "super-secret-key-for-development-only-min-32-chars")
            throw new InvalidOperationException(
                "JwtSettings:SecretKey must be configured in production. Do not use the default development key.");

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
    }

    private static void AddCorsPolicy(this WebApplicationBuilder builder)
    {
        var origins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000"];

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
                policy.WithOrigins(origins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });
    }

    private static void AddHealthCheckServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL") ?? "", name: "postgresql")
            .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis");
    }

    private static void AddSwaggerServices(this WebApplicationBuilder builder)
    {
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
                        Reference = new()
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    private static void AddRateLimitingPolicies(this WebApplicationBuilder builder)
    {
        var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();
                logger.LogWarning("Rate limit hit: {Path} user={UserId} ip={IP}",
                    context.HttpContext.Request.Path, userId ?? "anon", ip);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse.Fail("RATE_LIMITED", "Too many requests. Try again later."), ct);
            };

            // Per-user policy for authenticated endpoints
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

                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"ip:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
            });

            // Anonymous policy — generous per-IP (CGNAT-safe)
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
    }

    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        app.UseMiddleware<AutoTest.Api.Middleware.ExceptionHandlingMiddleware>();
        app.UseMiddleware<AutoTest.Api.Middleware.VaryHeaderMiddleware>();

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserId", httpContext.User?.FindFirst("sub")?.Value ?? "anonymous");
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            };
        });

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health").DisableRateLimiting();

        return app;
    }
}
