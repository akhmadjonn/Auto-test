using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Persistence;

public class DbSeeder(AppDbContext db, ICacheService cache, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        await SeedCategoriesAsync(ct);
        await SeedSystemSettingsAsync(ct);
        await SeedSubscriptionPlansAsync(ct);
        await SeedExamTemplateAsync(ct);
        await SeedAdminUserAsync(ct);

        await db.SaveChangesAsync(ct);

        // Populate Redis with all system settings so handlers don't rely on hardcoded fallbacks
        await SyncSettingsToRedisAsync(ct);

        logger.LogInformation("Database seeding completed");
    }

    private async Task SyncSettingsToRedisAsync(CancellationToken ct)
    {
        var settings = await db.SystemSettings.ToListAsync(ct);
        foreach (var s in settings)
            await cache.SetAsync($"avtolider:settings:{s.Key}", s.Value, TimeSpan.FromDays(1), ct);
        logger.LogInformation("Synced {Count} system settings to Redis", settings.Count);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct))
            return;

        // 28 categories — trilingual
        var categories = new List<Category>
        {
            // Parent categories
            MakeCategory("road-signs", "Йўл белгилари", "Yo'l belgilari", "Дорожные знаки", 1, null),
            MakeCategory("road-markings", "Йўл чизиқлари", "Yo'l chiziqlari", "Дорожная разметка", 2, null),
            MakeCategory("traffic-lights", "Светофор", "Svetofor", "Светофор", 3, null),
            MakeCategory("traffic-rules", "Ҳаракат қоидалари", "Harakatlanish qoidalari", "Правила движения", 4, null),
            MakeCategory("priority-rules", "Устунлик қоидалари", "Ustunlik qoidalari", "Правила приоритета", 5, null),
            MakeCategory("speed-limits", "Тезлик чегаралари", "Tezlik chegaralari", "Скоростной режим", 6, null),
            MakeCategory("overtaking", "Қувиб ўтиш", "Quvib o'tish", "Обгон", 7, null),
            MakeCategory("parking", "Тўхташ ва стоянка", "To'xtash va stoyanka", "Остановка и стоянка", 8, null),
            MakeCategory("intersections", "Чорраҳалар", "Chorrahalar", "Перекрёстки", 9, null),
            MakeCategory("pedestrians", "Пиёдалар", "Piyodalar", "Пешеходы", 10, null),
            MakeCategory("technical-requirements", "Техник талаблар", "Texnik talablar", "Технические требования", 11, null),
            MakeCategory("first-aid", "Биринчи тиббий ёрдам", "Birinchi tibbiy yordam", "Первая медицинская помощь", 12, null),
            MakeCategory("driver-responsibility", "Ҳайдовчи жавобгарлиги", "Haydovchi javobgarligi", "Ответственность водителя", 13, null),
            MakeCategory("vehicle-maintenance", "Техник хизмат", "Texnik xizmat", "Техническое обслуживание", 14, null),
            MakeCategory("environment", "Атроф-муҳит", "Atrof-muhit", "Окружающая среда", 15, null),
            MakeCategory("railway-crossings", "Темир йўл кесишмалари", "Temir yo'l kesishmalari", "Железнодорожные переезды", 16, null),
            MakeCategory("tunnels-bridges", "Тунеллар ва кўприклар", "Tunellar va ko'priklar", "Тоннели и мосты", 17, null),
            MakeCategory("adverse-conditions", "Қийин шароитлар", "Qiyin sharoitlar", "Сложные условия", 18, null),
            MakeCategory("towing", "Эвакуация ва буксир", "Evakuatsiya va buksir", "Эвакуация и буксировка", 19, null),
            MakeCategory("licensing", "Ҳайдовчилик гувоҳномаси", "Haydovchilik guvohnomasi", "Водительское удостоверение", 20, null),
            // Road signs subcategories
            MakeCategory("warning-signs", "Огоҳлантирувчи белгилар", "Ogohlantirivchi belgilar", "Предупреждающие знаки", 1, "road-signs"),
            MakeCategory("priority-signs", "Устунлик белгилари", "Ustunlik belgilari", "Знаки приоритета", 2, "road-signs"),
            MakeCategory("prohibitory-signs", "Тақиқловчи белгилар", "Taqiqlovchi belgilar", "Запрещающие знаки", 3, "road-signs"),
            MakeCategory("mandatory-signs", "Мажбурий белгилар", "Majburiy belgilar", "Предписывающие знаки", 4, "road-signs"),
            MakeCategory("informational-signs", "Ахборот белгилари", "Axborot belgilari", "Информационные знаки", 5, "road-signs"),
            MakeCategory("service-signs", "Хизмат белгилари", "Xizmat belgilari", "Знаки сервиса", 6, "road-signs"),
            MakeCategory("additional-panels", "Қўшимча плиталар", "Qo'shimcha plitalar", "Дополнительные таблички", 7, "road-signs"),
            MakeCategory("cd-specific", "CD тоифаси", "CD toifasi", "Категория CD", 8, null),
        };

        // Build proper parent references for road sign subcategories
        var roadSignsId = categories.First(c => c.Slug == "road-signs").Id;
        foreach (var sub in categories.Where(c => c.Slug is "warning-signs" or "priority-signs"
            or "prohibitory-signs" or "mandatory-signs" or "informational-signs"
            or "service-signs" or "additional-panels"))
            sub.ParentId = roadSignsId;

        db.Categories.AddRange(categories);
    }

    private async Task SeedSystemSettingsAsync(CancellationToken ct)
    {
        if (await db.SystemSettings.AnyAsync(ct))
            return;

        var settings = new List<SystemSetting>
        {
            new() { Key = "free_daily_exam_limit", Value = "3", Description = "Free daily exam limit for non-subscribers" },
            new() { Key = "otp_ttl_minutes", Value = "5", Description = "OTP code TTL in minutes" },
            new() { Key = "otp_rate_limit_count", Value = "3", Description = "Max OTP requests in window" },
            new() { Key = "otp_rate_limit_window_minutes", Value = "15", Description = "OTP rate limit window in minutes" },
            new() { Key = "otp_cooldown_seconds", Value = "60", Description = "Cooldown between OTP sends in seconds" },
            new() { Key = "max_active_sessions", Value = "1", Description = "Max concurrent active exam sessions per user" },
            new() { Key = "presigned_url_hours", Value = "1", Description = "MinIO presigned URL expiry in hours" },
            new() { Key = "payme_enabled", Value = "true", Description = "Enable Payme payment provider" },
            new() { Key = "click_enabled", Value = "true", Description = "Enable Click payment provider" },
            new() { Key = "sms_provider", Value = "eskiz", Description = "SMS provider (eskiz)" },
            new() { Key = "maintenance_mode", Value = "false", Description = "Enable maintenance mode" },
            new() { Key = "min_app_version", Value = "1.0.0", Description = "Minimum supported app version" },
            new() { Key = "max_exams_per_day", Value = "50", Description = "Max exam sessions per day per user" },
            new() { Key = "otp_expiry_seconds", Value = "300", Description = "OTP expiry in seconds" },
            new() { Key = "otp_max_attempts", Value = "3", Description = "Max OTP verification attempts" },
        };

        db.SystemSettings.AddRange(settings);
    }

    private async Task SeedSubscriptionPlansAsync(CancellationToken ct)
    {
        if (await db.SubscriptionPlans.AnyAsync(ct))
            return;

        var plans = new List<SubscriptionPlan>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = new LocalizedText("Haftalik", "Haftalik", "Недельный"),
                Description = new LocalizedText("7 kunlik to'liq kirish", "7 kunlik to'liq kirish", "7 дней полного доступа"),
                PriceInTiyins = 2_500_000,
                DurationDays = 7,
                Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\"]",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = new LocalizedText("Oylik", "Oylik", "Месячный"),
                Description = new LocalizedText("30 kunlik to'liq kirish", "30 kunlik to'liq kirish", "30 дней полного доступа"),
                PriceInTiyins = 6_000_000,
                DurationDays = 30,
                Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\",\"Spaced repetition\"]",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = new LocalizedText("Kurs", "Kurs", "Курс"),
                Description = new LocalizedText("90 kunlik to'liq kirish", "90 kunlik to'liq kirish", "90 дней полного доступа"),
                PriceInTiyins = 25_000_000,
                DurationDays = 90,
                Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\",\"Spaced repetition\",\"Tahlil\"]",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        db.SubscriptionPlans.AddRange(plans);
    }

    private async Task SeedExamTemplateAsync(CancellationToken ct)
    {
        if (await db.ExamTemplates.AnyAsync(ct))
            return;

        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("Standart imtihon", "Standart imtihon", "Стандартный экзамен"),
            TotalQuestions = 20,
            PassingScore = 80,
            TimeLimitMinutes = 20,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.ExamTemplates.Add(template);
    }

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
            return;

        var admin = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+998901234567",
            FirstName = "Admin",
            LastName = "Avtolider",
            Role = UserRole.Admin,
            AuthProvider = AuthProvider.Phone,
            PreferredLanguage = Language.UzLatin,
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(admin);
    }

    private static Category MakeCategory(
        string slug, string uz, string uzLatin, string ru,
        int sort, string? parentSlug)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = new LocalizedText(uz, uzLatin, ru),
            Description = new LocalizedText(uz, uzLatin, ru),
            SortOrder = sort,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
