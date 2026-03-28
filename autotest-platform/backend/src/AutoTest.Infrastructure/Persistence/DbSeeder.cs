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

        // Persist categories + template so pool rules and questions can query them
        await db.SaveChangesAsync(ct);

        await SeedExamPoolRulesAsync(ct);
        await SeedQuestionsAsync(ct);
        await SeedAdminUserAsync(ct);

        await db.SaveChangesAsync(ct);

        // Phase 2 content seeding
        await SeedTrafficFinesAsync(ct);
        await SeedHazardLabelsAsync(ct);
        await SeedFirstAidProceduresAsync(ct);
        await SeedGlossaryCategoriesAndTermsAsync(ct);

        await db.SaveChangesAsync(ct);

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

        // 28 official PDD (YHQ) chapters + road-signs parent + CD + uncategorized
        var categories = new List<Category>
        {
            // PDD Chapter 1: Атамалар / Термины
            MakeCategory("terms", "Атамалар", "Atamalar", "Термины", 1, null),
            // PDD Chapter 2: Мажбуриятлар / Обязанности участников
            MakeCategory("participant-duties", "Йўл ҳаракати қатнашчиларининг мажбуриятлари", "Yo'l harakati qatnashchilarining majburiyatlari", "Обязанности участников дорожного движения", 2, null),
            // PDD Chapter 3: Светофор ва тартибга солувчи / Светофор и регулировщик
            MakeCategory("traffic-lights", "Светофор ва тартибга солувчи ишоралари", "Svetofor va tartibga soluvchi ishoralari", "Сигналы светофора и регулировщика", 3, null),
            // PDD Chapter 4: Огоҳлантирувчи ва фалокат ишоралари / Предупредительные и аварийные сигналы
            MakeCategory("warning-signals", "Огоҳлантирувчи ва фалокат ишоралари", "Ogohlantirivchi va falokat ishoralari", "Предупредительные и аварийные сигналы", 4, null),
            // PDD Chapter 5: Таниқлилик белгилари / Опознавательные знаки ТС
            MakeCategory("vehicle-id-signs", "Транспорт воситаларининг таниқлилик белгилари", "Transport vositalarining taniqlilik belgilari", "Опознавательные знаки транспортных средств", 5, null),
            // Road signs parent group (chapters 6-10)
            MakeCategory("road-signs", "Йўл белгилари", "Yo'l belgilari", "Дорожные знаки", 6, null),
            // PDD Chapter 6: Огоҳлантирувчи белгилар / Предупреждающие знаки
            MakeCategory("warning-signs", "Огоҳлантирувчи белгилар", "Ogohlantirivchi belgilar", "Предупреждающие знаки", 1, "road-signs"),
            // PDD Chapter 7: Имтиёз белгилари / Знаки приоритета
            MakeCategory("priority-signs", "Имтиёз белгилари", "Imtiyoz belgilari", "Знаки приоритета", 2, "road-signs"),
            // PDD Chapter 8: Тақиқловчи белгилар / Запрещающие знаки
            MakeCategory("prohibitory-signs", "Тақиқловчи белгилар", "Taqiqlovchi belgilar", "Запрещающие знаки", 3, "road-signs"),
            // PDD Chapter 9: Буюрувчи белгилар / Предписывающие знаки
            MakeCategory("mandatory-signs", "Буюрувчи белгилар", "Buyuruvchi belgilar", "Предписывающие знаки", 4, "road-signs"),
            // PDD Chapter 10: Ахборот, сервис ва қўшимча белгилар / Информационно-указательные, сервисные и доп. знаки
            MakeCategory("informational-signs", "Ахборот, сервис ва қўшимча белгилар", "Axborot, servis va qo'shimcha belgilar", "Информационно-указательные, сервисные и доп. знаки", 5, "road-signs"),
            // PDD Chapter 11: Йўл чизиқлари / Дорожные разметки
            MakeCategory("road-markings", "Йўл чизиқлари", "Yo'l chiziqlari", "Дорожные разметки", 7, null),
            // PDD Chapter 12: Ҳаракатни бошлаш ва йўналишни ўзгартириш / Начало движения и изменение направления
            MakeCategory("starting-direction", "Ҳаракатни бошлаш ва йўналишни ўзгартириш", "Harakatni boshlash va yo'nalishni o'zgartirish", "Начало движения и изменение направления", 8, null),
            // PDD Chapter 13: ТВ жойлашуви / Расположение ТС на проезжей части
            MakeCategory("vehicle-positioning", "Транспорт воситаларининг жойлашуви", "Transport vositalarining joylashuvi", "Расположение транспортных средств на проезжей части", 9, null),
            // PDD Chapter 14: Тезлик чегаралари / Скорость движения
            MakeCategory("speed-limits", "Ҳаракатланиш тезлиги", "Harakatlanish tezligi", "Скорость движения", 10, null),
            // PDD Chapter 15: Тўхташ ва стоянка / Остановка и стоянка
            MakeCategory("parking", "Тўхташ ва тўхтаб туриш", "To'xtash va to'xtab turish", "Остановка и стоянка", 11, null),
            // PDD Chapter 16: Қувиб ўтиш / Обгон
            MakeCategory("overtaking", "Қувиб ўтиш", "Quvib o'tish", "Обгон", 12, null),
            // PDD Chapter 17: Тенг аҳамиятли чоррахалар / Равнозначные перекрёстки
            MakeCategory("equal-intersections", "Тенг аҳамиятли чоррахалар", "Teng ahamiyatli chorrahalar", "Равнозначные перекрёстки", 13, null),
            // PDD Chapter 18: Тартибга солинмаган чоррахалар / Нерегулируемые перекрёстки (со знаками приоритета)
            MakeCategory("unregulated-intersections", "Тартибга солинмаган (имтиёз белгили) чоррахалар", "Tartibga solinmagan (imtiyoz belgili) chorrahalar", "Нерегулируемые перекрёстки (со знаками приоритета)", 14, null),
            // PDD Chapter 19: Тартибга солинган чоррахалар / Регулируемые перекрёстки (со светофором)
            MakeCategory("regulated-intersections", "Тартибга солинган (светофорли) чоррахалар", "Tartibga solingan (svetoforli) chorrahalar", "Регулируемые перекрёстки (со светофором)", 15, null),
            // PDD Chapter 20: Темир йўл кесишмалари / Движение через железнодорожные пути
            MakeCategory("railway-crossings", "Темир йўл кесишмалари орқали ҳаракатланиш", "Temir yo'l kesishmalari orqali harakatlanish", "Движение через железнодорожные пути", 16, null),
            // PDD Chapter 21: Автомагистрал / Движение по автомагистралям
            MakeCategory("highway-driving", "Автомагистралда ҳаракатланиш", "Avtomagistralda harakatlanish", "Движение по автомагистралям", 17, null),
            // PDD Chapter 22: Ташқи ёритиш чироқлари / Внешние световые приборы
            MakeCategory("external-lights", "Ташқи ёритиш чироқлари", "Tashqi yoritish chiroqlari", "Внешние световые приборы", 18, null),
            // PDD Chapter 23: Шатакка олиш / Буксировка
            MakeCategory("towing", "Шатакка олиш", "Shatakka olish", "Буксировка механических транспортных средств", 19, null),
            // PDD Chapter 24: Одам ташиш / Перевозка людей
            MakeCategory("passenger-transport", "Одам ташиш", "Odam tashish", "Перевозка людей", 20, null),
            // PDD Chapter 25: Юк ташиш / Перевозка грузов
            MakeCategory("cargo-transport", "Юк ташиш", "Yuk tashish", "Перевозка грузов", 21, null),
            // PDD Chapter 26: ТВ фойдаланишни тақиқловчи шартлар / Условия запрещения эксплуатации ТС
            MakeCategory("technical-requirements", "Транспорт воситаларидан фойдаланишни тақиқловчи шартлар", "Transport vositalaridan foydalanishni taqiqlovchi shartlar", "Условия запрещения эксплуатации транспортных средств", 22, null),
            // PDD Chapter 27: Ҳаракат хавфсизлиги асослари / Безопасность управления
            MakeCategory("driving-safety", "Ҳаракат хавфсизлиги асослари", "Harakat xavfsizligi asoslari", "Безопасность управления автомобилем", 23, null),
            // PDD Chapter 28: Биринчи тиббий ёрдам / Первая медицинская помощь
            MakeCategory("first-aid", "Биринчи тиббий ёрдам кўрсатиш", "Birinchi tibbiy yordam ko'rsatish", "Первая медицинская помощь", 24, null),
            // Extra categories
            MakeCategory("cd-specific", "CD тоифаси", "CD toifasi", "Категория CD", 25, null),
            MakeCategory("uncategorized", "Таснифланмаган", "Tasniflanmagan", "Без категории", 99, null),
        };

        var roadSignsId = categories.First(c => c.Slug == "road-signs").Id;
        foreach (var sub in categories.Where(c => c.Slug is "warning-signs" or "priority-signs"
            or "prohibitory-signs" or "mandatory-signs" or "informational-signs"))
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

        var now = DateTimeOffset.UtcNow;
        var plans = new List<SubscriptionPlan>
        {
            new() { Id = Guid.NewGuid(), Name = new LocalizedText("Haftalik", "Haftalik", "Недельный"), Description = new LocalizedText("7 kunlik to'liq kirish", "7 kunlik to'liq kirish", "7 дней полного доступа"), PriceInTiyins = 2_500_000, DurationDays = 7, Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\"]", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = new LocalizedText("Oylik", "Oylik", "Месячный"), Description = new LocalizedText("30 kunlik to'liq kirish", "30 kunlik to'liq kirish", "30 дней полного доступа"), PriceInTiyins = 6_000_000, DurationDays = 30, Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\",\"Spaced repetition\"]", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new() { Id = Guid.NewGuid(), Name = new LocalizedText("Kurs", "Kurs", "Курс"), Description = new LocalizedText("90 kunlik to'liq kirish", "90 kunlik to'liq kirish", "90 дней полного доступа"), PriceInTiyins = 25_000_000, DurationDays = 90, Features = "[\"Cheksiz imtihon\",\"Barcha savollar\",\"Statistika\",\"Spaced repetition\",\"Tahlil\"]", IsActive = true, CreatedAt = now, UpdatedAt = now },
        };

        db.SubscriptionPlans.AddRange(plans);
    }

    private async Task SeedExamTemplateAsync(CancellationToken ct)
    {
        if (await db.ExamTemplates.AnyAsync(ct))
            return;

        db.ExamTemplates.Add(new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("Standart imtihon", "Standart imtihon", "Стандартный экзамен"),
            TotalQuestions = 20,
            PassingScore = 90,
            TimeLimitMinutes = 25,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task SeedExamPoolRulesAsync(CancellationToken ct)
    {
        if (await db.ExamPoolRules.AnyAsync(ct))
            return;

        var template = await db.ExamTemplates.FirstAsync(t => t.IsActive, ct);
        var now = DateTimeOffset.UtcNow;

        // Pull 20 random questions from any active category for the exam
        var anyCategory = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "uncategorized", ct);
        if (anyCategory is null)
        {
            logger.LogWarning("Category 'uncategorized' not found, skipping pool rules");
            return;
        }

        db.ExamPoolRules.Add(MakePoolRule(template.Id, anyCategory.Id, null, 20, now));
    }

    private async Task SeedQuestionsAsync(CancellationToken ct)
    {
        if (await db.Questions.AnyAsync(ct))
            return;

        var cats = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);
        var now = DateTimeOffset.UtcNow;
        var q = new List<Question>();

        // ── road-signs: 8 questions (tickets 1-3) ──
        q.Add(Q(cats["road-signs"].Id, 1, Difficulty.Easy, now,
            T("Учбурчак шаклидаги оқ-қизил белги нимани англатади?", "Uchburchak shaklidagi oq-qizil belgi nimani anglatadi?", "Что означает треугольный бело-красный знак?"),
            T("Бу огоҳлантирувчи белги бўлиб, хавф тўғрисида огоҳлантиради.", "Bu ogohlantirivchi belgi bo'lib, xavf to'g'risida ogohlantiradi.", "Это предупреждающий знак, он предупреждает об опасности."),
            A("Огоҳлантирувчи белги", "Ogohlantirivchi belgi", "Предупреждающий знак", true),
            A("Тақиқловчи белги", "Taqiqlovchi belgi", "Запрещающий знак", false),
            A("Ахборот белгиси", "Axborot belgisi", "Информационный знак", false),
            A("Хизмат белгиси", "Xizmat belgisi", "Знак сервиса", false)));

        q.Add(Q(cats["road-signs"].Id, 1, Difficulty.Medium, now,
            T("Думалоқ кўк белги оқ ўқ билан нимани англатади?", "Dumaloq ko'k belgi oq o'q bilan nimani anglatadi?", "Что означает круглый синий знак с белой стрелкой?"),
            T("Мажбурий ҳаракат йўналишини кўрсатади.", "Majburiy harakat yo'nalishini ko'rsatadi.", "Указывает обязательное направление движения."),
            A("Мажбурий йўналиш", "Majburiy yo'nalish", "Обязательное направление", true),
            A("Тақиқланган йўналиш", "Taqiqlangan yo'nalish", "Запрещённое направление", false),
            A("Тавсия этилган йўналиш", "Tavsiya etilgan yo'nalish", "Рекомендуемое направление", false),
            A("Вақтинча йўналиш", "Vaqtincha yo'nalish", "Временное направление", false)));

        q.Add(Q(cats["road-signs"].Id, 1, Difficulty.Hard, now,
            T("Қизил доира ичида велосипед тасвири қайси белгига тегишли?", "Qizil doira ichida velosiped tasviri qaysi belgiga tegishli?", "К какому знаку относится изображение велосипеда в красном круге?"),
            T("Велосипед ҳаракати тақиқланган жойни кўрсатади.", "Velosiped harakati taqiqlangan joyni ko'rsatadi.", "Показывает место, где запрещено движение велосипедов."),
            A("Велосипед ҳаракати тақиқланган", "Velosiped harakati taqiqlangan", "Движение велосипедов запрещено", true),
            A("Велосипед йўлакчаси", "Velosiped yo'lakchasi", "Велосипедная дорожка", false),
            A("Велосипед тўхташ жойи", "Velosiped to'xtash joyi", "Место остановки велосипедов", false),
            A("Велосипед ижараси", "Velosiped ijarasi", "Прокат велосипедов", false)));

        q.Add(Q(cats["road-signs"].Id, 2, Difficulty.Easy, now,
            T("Саккизбурчак қизил белги нимани англатади?", "Sakkizburchak qizil belgi nimani anglatadi?", "Что означает восьмиугольный красный знак?"),
            T("STOP — тўлиқ тўхташ мажбурий.", "STOP — to'liq to'xtash majburiy.", "STOP — полная остановка обязательна."),
            A("Тўхташ мажбурий", "To'xtash majburiy", "Остановка обязательна", true),
            A("Секин юриш", "Sekin yurish", "Медленное движение", false),
            A("Йўл беринг", "Yo'l bering", "Уступите дорогу", false),
            A("Кириш тақиқланган", "Kirish taqiqlangan", "Въезд запрещён", false)));

        q.Add(Q(cats["road-signs"].Id, 2, Difficulty.Medium, now,
            T("Тескари учбурчак белги нимани англатади?", "Teskari uchburchak belgi nimani anglatadi?", "Что означает перевёрнутый треугольный знак?"),
            T("Йўл беринг белгиси — қарши транспортга йўл бериш керак.", "Yo'l bering belgisi — qarshi transportga yo'l berish kerak.", "Знак «Уступите дорогу» — нужно уступить встречному транспорту."),
            A("Йўл беринг", "Yo'l bering", "Уступите дорогу", true),
            A("Асосий йўл", "Asosiy yo'l", "Главная дорога", false),
            A("Тўхтанг", "To'xtang", "Стоп", false),
            A("Ҳаракат тақиқланган", "Harakat taqiqlangan", "Движение запрещено", false)));

        q.Add(Q(cats["road-signs"].Id, 3, Difficulty.Easy, now,
            T("Кўк квадрат ичида оқ P ҳарфи нимани англатади?", "Ko'k kvadrat ichida oq P harfi nimani anglatadi?", "Что означает белая буква P в синем квадрате?"),
            T("Автомобил тўхташ жойи — парковка рухсат этилган.", "Avtomobil to'xtash joyi — parkovka ruxsat etilgan.", "Место стоянки автомобилей — парковка разрешена."),
            A("Парковка рухсат этилган", "Parkovka ruxsat etilgan", "Парковка разрешена", true),
            A("Парковка тақиқланган", "Parkovka taqiqlangan", "Парковка запрещена", false),
            A("Пуллик парковка", "Pullik parkovka", "Платная парковка", false),
            A("Фақат ногиронлар учун", "Faqat nogironlar uchun", "Только для инвалидов", false)));

        q.Add(Q(cats["road-signs"].Id, 3, Difficulty.Medium, now,
            T("Сариқ ромб шаклидаги белги нимани англатади?", "Sariq romb shaklidagi belgi nimani anglatadi?", "Что означает жёлтый ромбовидный знак?"),
            T("Асосий йўл белгиси — устунлик ҳуқуқини беради.", "Asosiy yo'l belgisi — ustunlik huquqini beradi.", "Знак главной дороги — даёт приоритет."),
            A("Асосий йўл", "Asosiy yo'l", "Главная дорога", true),
            A("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости", false),
            A("Огоҳлантириш", "Ogohlantirish", "Предупреждение", false),
            A("Қўшимча плита", "Qo'shimcha plita", "Дополнительная табличка", false)));

        q.Add(Q(cats["road-signs"].Id, 2, Difficulty.Hard, now,
            T("Оқ доира ичида қизил чегара белгиси нимани англатади?", "Oq doira ichida qizil chegara belgisi nimani anglatadi?", "Что означает знак с красной каймой в белом круге?"),
            T("Ҳаракат тақиқланган — барча транспорт воситаларига кириш ман.", "Harakat taqiqlangan — barcha transport vositalariga kirish man.", "Движение запрещено — въезд запрещён для всех транспортных средств."),
            A("Ҳаракат тақиқланган", "Harakat taqiqlangan", "Движение запрещено", true),
            A("Тезлик чегараси йўқ", "Tezlik chegarasi yo'q", "Нет ограничения скорости", false),
            A("Ҳудуд чегараси", "Hudud chegarasi", "Граница территории", false),
            A("Тақиқларнинг тугаши", "Taqiqlarning tugashi", "Конец запретов", false)));

        // ── traffic-rules: 8 questions ──
        q.Add(Q(cats["participant-duties"].Id, 1, Difficulty.Easy, now,
            T("Ўнг томонлама ҳаракат қоидаси нимани англатади?", "O'ng tomonlama harakat qoidasi nimani anglatadi?", "Что означает правило правостороннего движения?"),
            T("Транспорт воситалари йўлнинг ўнг томонида ҳаракатланиши керак.", "Transport vositalari yo'lning o'ng tomonida harakatlanishi kerak.", "Транспортные средства должны двигаться по правой стороне дороги."),
            A("Йўлнинг ўнг томонида юриш", "Yo'lning o'ng tomonida yurish", "Движение по правой стороне", true),
            A("Йўлнинг чап томонида юриш", "Yo'lning chap tomonida yurish", "Движение по левой стороне", false),
            A("Йўлнинг ўртасида юриш", "Yo'lning o'rtasida yurish", "Движение по центру", false),
            A("Ихтиёрий томонда юриш", "Ixtiyoriy tomonda yurish", "Движение по любой стороне", false)));

        q.Add(Q(cats["participant-duties"].Id, 1, Difficulty.Medium, now,
            T("Ҳайдовчи йўналиш кўрсаткичини қачон ёқиши керак?", "Haydovchi yo'nalish ko'rsatkichini qachon yoqishi kerak?", "Когда водитель должен включить указатель поворота?"),
            T("Манёвр бошланишидан олдин, бошқа транспорт воситаларини огоҳлантириш учун.", "Manevr boshlanishidan oldin, boshqa transport vositalarini ogohlantirish uchun.", "Перед началом манёвра, чтобы предупредить другие транспортные средства."),
            A("Манёврдан олдин", "Manevrdan oldin", "Перед манёвром", true),
            A("Манёвр вақтида", "Manevr vaqtida", "Во время манёвра", false),
            A("Манёвр тугагач", "Manevr tugagach", "После манёвра", false),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false)));

        q.Add(Q(cats["participant-duties"].Id, 2, Difficulty.Easy, now,
            T("Хавфсизлик камари тақиш мажбуриями?", "Xavfsizlik kamari taqish majburiymi?", "Обязательно ли пристёгиваться ремнём безопасности?"),
            T("Ҳа, ҳайдовчи ва барча йўловчилар хавфсизлик камарини тақиши шарт.", "Ha, haydovchi va barcha yo'lovchilar xavfsizlik kamarini taqishi shart.", "Да, водитель и все пассажиры обязаны пристёгиваться."),
            A("Ҳа, барчага мажбурий", "Ha, barchaga majburiy", "Да, обязательно для всех", true),
            A("Фақат ҳайдовчига", "Faqat haydovchiga", "Только для водителя", false),
            A("Фақат шаҳарда", "Faqat shaharda", "Только в городе", false),
            A("Мажбурий эмас", "Majburiy emas", "Не обязательно", false)));

        q.Add(Q(cats["participant-duties"].Id, 2, Difficulty.Medium, now,
            T("Қайси ҳолатда чап томонга буриш тақиқланади?", "Qaysi holatda chap tomonga burish taqiqlanadi?", "В каком случае запрещён поворот налево?"),
            T("Тақиқловчи белги ёки йўл чизиғи чап буришни тақиқлаган ҳолатда.", "Taqiqlovchi belgi yoki yo'l chizig'i chap burishni taqiqlagan holatda.", "При наличии запрещающего знака или разметки, запрещающей поворот налево."),
            A("Тақиқловчи белги мавжуд бўлганда", "Taqiqlovchi belgi mavjud bo'lganda", "При наличии запрещающего знака", true),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false),
            A("Ёмғир пайтида", "Yomg'ir paytida", "Во время дождя", false),
            A("Ҳеч қачон тақиқланмайди", "Hech qachon taqiqlanmaydi", "Никогда не запрещается", false)));

        q.Add(Q(cats["participant-duties"].Id, 3, Difficulty.Hard, now,
            T("Тезликни камайтирмасдан бурилиш нимага олиб келади?", "Tezlikni kamaytirmasdan burilish nimaga olib keladi?", "К чему приводит поворот без снижения скорости?"),
            T("Автомобил бошқарувдан чиқиши ва ағдарилиши мумкин.", "Avtomobil boshqaruvdan chiqishi va ag'darilishi mumkin.", "Автомобиль может потерять управление и опрокинуться."),
            A("Бошқарувдан чиқиш", "Boshqaruvdan chiqish", "Потеря управления", true),
            A("Ёнилғи тежаш", "Yonilg'i tejash", "Экономия топлива", false),
            A("Тезроқ бурилиш", "Tezroq burilish", "Более быстрый поворот", false),
            A("Ҳеч нарса бўлмайди", "Hech narsa bo'lmaydi", "Ничего не произойдёт", false)));

        q.Add(Q(cats["participant-duties"].Id, 3, Difficulty.Medium, now,
            T("Кечаси аҳоли пункти ичида қайси чироқ ёқилади?", "Kechasi aholi punkti ichida qaysi chiroq yoqiladi?", "Какой свет включается ночью в населённом пункте?"),
            T("Яқинни ёритувчи фаралар ёқилади.", "Yaqinni yorituvchi faralar yoqiladi.", "Включается ближний свет фар."),
            A("Яқинни ёритувчи фара", "Yaqinni yorituvchi fara", "Ближний свет", true),
            A("Узоқни ёритувчи фара", "Uzoqni yorituvchi fara", "Дальний свет", false),
            A("Туман фаралари", "Tuman faralari", "Противотуманные фары", false),
            A("Чироқлар ёқилмайди", "Chiroqlar yoqilmaydi", "Свет не включается", false)));

        q.Add(Q(cats["participant-duties"].Id, 1, Difficulty.Hard, now,
            T("Икки йўлли йўлда қувиб ўтиш қачон тақиқланади?", "Ikki yo'lli yo'lda quvib o'tish qachon taqiqlanadi?", "Когда запрещён обгон на двухполосной дороге?"),
            T("Кўринмаслик, чорраҳалар олдида, тепаликларда ва тақиқловчи белги бўлганда.", "Ko'rinmaslik, chorrahalar oldida, tepaliklarda va taqiqlovchi belgi bo'lganda.", "При плохой видимости, перед перекрёстками, на подъёмах и при наличии запрещающего знака."),
            A("Кўринмаслик ва чорраҳалар олдида", "Ko'rinmaslik va chorrahalar oldida", "При плохой видимости и перед перекрёстками", true),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false),
            A("Фақат ёмғирда", "Faqat yomg'irda", "Только в дождь", false),
            A("Қувиб ўтиш доимо рухсат", "Quvib o'tish doimo ruxsat", "Обгон всегда разрешён", false)));

        q.Add(Q(cats["participant-duties"].Id, 3, Difficulty.Easy, now,
            T("Ҳайдовчи телефонда гаплашиши мумкинми?", "Haydovchi telefonda gaplashishi mumkinmi?", "Может ли водитель разговаривать по телефону?"),
            T("Фақат гарнитура ёки громкая связь орқали рухсат.", "Faqat garnitura yoki gromkaya svyaz orqali ruxsat.", "Разрешено только через гарнитуру или громкую связь."),
            A("Фақат гарнитура билан", "Faqat garnitura bilan", "Только с гарнитурой", true),
            A("Ҳа, доимо", "Ha, doimo", "Да, всегда", false),
            A("Йўқ, ҳеч қачон", "Yo'q, hech qachon", "Нет, никогда", false),
            A("Фақат тўхтаб турганда", "Faqat to'xtab turganda", "Только на остановке", false)));

        // ── traffic-lights: 6 questions ──
        q.Add(Q(cats["traffic-lights"].Id, 1, Difficulty.Easy, now,
            T("Қизил светофор сигнали нимани англатади?", "Qizil svetofor signali nimani anglatadi?", "Что означает красный сигнал светофора?"),
            T("Ҳаракат тўлиқ тақиқланган.", "Harakat to'liq taqiqlangan.", "Движение полностью запрещено."),
            A("Тўхтанг", "To'xtang", "Стоп", true),
            A("Секин юринг", "Sekin yuring", "Двигайтесь медленно", false),
            A("Тезлатинг", "Tezlating", "Ускорьтесь", false),
            A("Диққат", "Diqqat", "Внимание", false)));

        q.Add(Q(cats["traffic-lights"].Id, 1, Difficulty.Medium, now,
            T("Яшил милтилловчи сигнал нимани англатади?", "Yashil miltillovchi signal nimani anglatadi?", "Что означает мигающий зелёный сигнал?"),
            T("Яшил сигнал тугамоқда, тўхташга тайёрланинг.", "Yashil signal tugamoqda, to'xtashga tayyorlaning.", "Зелёный сигнал заканчивается, готовьтесь к остановке."),
            A("Сигнал тугамоқда", "Signal tugamoqda", "Сигнал заканчивается", true),
            A("Тезлатинг", "Tezlating", "Ускорьтесь", false),
            A("Тўхтанг", "To'xtang", "Остановитесь", false),
            A("Йўл беринг", "Yo'l bering", "Уступите дорогу", false)));

        q.Add(Q(cats["traffic-lights"].Id, 2, Difficulty.Easy, now,
            T("Сариқ светофор сигнали нимани англатади?", "Sariq svetofor signali nimani anglatadi?", "Что означает жёлтый сигнал светофора?"),
            T("Диққат — сигнал алмашинмоқда, ҳаракатни давом этмасдан кутинг.", "Diqqat — signal almashinmoqda, harakatni davom etmasdan kuting.", "Внимание — сигнал переключается, ожидайте не продолжая движение."),
            A("Диққат, кутинг", "Diqqat, kuting", "Внимание, ожидайте", true),
            A("Тезлатинг", "Tezlating", "Ускорьтесь", false),
            A("Тўхтанг", "To'xtang", "Остановитесь", false),
            A("Буринг", "Buring", "Поверните", false)));

        q.Add(Q(cats["traffic-lights"].Id, 2, Difficulty.Medium, now,
            T("Қўшимча бўлимли светофорда яшил ўқ нимани англатади?", "Qo'shimcha bo'limli svetoforda yashil o'q nimani anglatadi?", "Что означает зелёная стрелка в дополнительной секции светофора?"),
            T("Ўқ йўналишида ҳаракат рухсат этилган.", "O'q yo'nalishida harakat ruxsat etilgan.", "Движение в направлении стрелки разрешено."),
            A("Ўқ йўналишида рухсат", "O'q yo'nalishida ruxsat", "Разрешено в направлении стрелки", true),
            A("Барча йўналишда рухсат", "Barcha yo'nalishda ruxsat", "Разрешено во всех направлениях", false),
            A("Тўхтанг", "To'xtang", "Остановитесь", false),
            A("Пиёдаларга рухсат", "Piyodalarga ruxsat", "Разрешено пешеходам", false)));

        q.Add(Q(cats["traffic-lights"].Id, 3, Difficulty.Hard, now,
            T("Светофор ишламаётган бўлса, чорраҳа қандай ҳисобланади?", "Svetofor ishlamayotgan bo'lsa, chorraha qanday hisoblanadi?", "Как считается перекрёсток, если светофор не работает?"),
            T("Тартибга солинмаган чорраҳа сифатида кўрилади.", "Tartibga solinmagan chorraha sifatida ko'riladi.", "Считается нерегулируемым перекрёстком."),
            A("Тартибга солинмаган чорраҳа", "Tartibga solinmagan chorraha", "Нерегулируемый перекрёсток", true),
            A("Тўхтаб туриш керак", "To'xtab turish kerak", "Нужно стоять", false),
            A("Асосий йўл ўтади", "Asosiy yo'l o'tadi", "Проезжает главная дорога", false),
            A("Ҳаракат тақиқланади", "Harakat taqiqlanadi", "Движение запрещено", false)));

        q.Add(Q(cats["traffic-lights"].Id, 3, Difficulty.Medium, now,
            T("Сариқ милтилловчи сигнал нимани англатади?", "Sariq miltillovchi signal nimani anglatadi?", "Что означает мигающий жёлтый сигнал?"),
            T("Эҳтиёт бўлинг, тартибга солинмаган чорраҳа.", "Ehtiyot bo'ling, tartibga solinmagan chorraha.", "Будьте осторожны, нерегулируемый перекрёсток."),
            A("Эҳтиёт бўлинг", "Ehtiyot bo'ling", "Будьте осторожны", true),
            A("Тезлатинг", "Tezlating", "Ускорьтесь", false),
            A("Тўхтанг", "To'xtang", "Остановитесь", false),
            A("Сигнал бузилган", "Signal buzilgan", "Сигнал сломан", false)));

        // ── priority-rules: 6 questions ──
        q.Add(Q(cats["equal-intersections"].Id, 1, Difficulty.Easy, now,
            T("Асосий йўлда ҳаракатланаётган транспортнинг устунлиги борми?", "Asosiy yo'lda harakatlanayotgan transportning ustunligi bormi?", "Имеет ли приоритет транспорт на главной дороге?"),
            T("Ҳа, асосий йўлдаги транспорт доимо устунликка эга.", "Ha, asosiy yo'ldagi transport doimo ustunlikka ega.", "Да, транспорт на главной дороге всегда имеет приоритет."),
            A("Ҳа, доимо", "Ha, doimo", "Да, всегда", true), A("Йўқ", "Yo'q", "Нет", false), A("Фақат кундузи", "Faqat kunduzi", "Только днём", false), A("Фақат шаҳарда", "Faqat shaharda", "Только в городе", false)));

        q.Add(Q(cats["equal-intersections"].Id, 2, Difficulty.Medium, now,
            T("Тенг аҳамиятли йўлларда ким биринчи ўтади?", "Teng ahamiyatli yo'llarda kim birinchi o'tadi?", "Кто проезжает первым на равнозначных дорогах?"),
            T("Ўнг томондан келаётган транспорт устунликка эга.", "O'ng tomondan kelayotgan transport ustunlikka ega.", "Приоритет имеет транспорт, приближающийся справа."),
            A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Приближающийся справа", true), A("Чапдан келаётган", "Chapdan kelayotgan", "Приближающийся слева", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Двигающийся быстрее", false), A("Каттароқ транспорт", "Kattaroq transport", "Более крупный транспорт", false)));

        q.Add(Q(cats["equal-intersections"].Id, 2, Difficulty.Hard, now,
            T("Тартибга солувчи ва светофор сигнали қарама-қарши бўлса, кимга бўйсуниш керак?", "Tartibga soluvchi va svetofor signali qarama-qarshi bo'lsa, kimga bo'ysunish kerak?", "Если сигналы регулировщика и светофора противоречат, кому подчиняться?"),
            T("Тартибга солувчининг сигналларига бўйсуниш керак.", "Tartibga soluvchining signallariga bo'ysunish kerak.", "Необходимо подчиняться сигналам регулировщика."),
            A("Тартибга солувчига", "Tartibga soluvchiga", "Регулировщику", true), A("Светофорга", "Svetoforga", "Светофору", false), A("Белгиларга", "Belgilarga", "Знакам", false), A("Ўзингиз қарор қилинг", "O'zingiz qaror qiling", "Решайте сами", false)));

        q.Add(Q(cats["equal-intersections"].Id, 3, Difficulty.Easy, now,
            T("Тез ёрдам автомобили сиренаси билан келаётганда нима қилиш керак?", "Tez yordam avtomobili sirenasi bilan kelayotganda nima qilish kerak?", "Что делать, когда приближается скорая помощь с сиреной?"),
            T("Йўл бериш ва четга чиқиш керак.", "Yo'l berish va chetga chiqish kerak.", "Необходимо уступить дорогу и съехать в сторону."),
            A("Йўл бериш", "Yo'l berish", "Уступить дорогу", true), A("Тезлатиш", "Tezlatish", "Ускориться", false), A("Тўхтаб туриш", "To'xtab turish", "Стоять на месте", false), A("Сигнал бериш", "Signal berish", "Подать сигнал", false)));

        q.Add(Q(cats["equal-intersections"].Id, 3, Difficulty.Medium, now,
            T("Доира ҳаракатли чорраҳада ким устунликка эга?", "Doira harakatli chorrahada kim ustunlikka ega?", "Кто имеет приоритет на круговом перекрёстке?"),
            T("Доира ичида ҳаракатланаётган транспорт устунликка эга.", "Doira ichida harakatlanayotgan transport ustunlikka ega.", "Приоритет имеет транспорт, движущийся по кругу."),
            A("Доира ичидаги транспорт", "Doira ichidagi transport", "Транспорт на кругу", true), A("Кираётган транспорт", "Kirayotgan transport", "Въезжающий транспорт", false), A("Катта транспорт", "Katta transport", "Крупный транспорт", false), A("Чапдан келаётган", "Chapdan kelayotgan", "Приближающийся слева", false)));

        q.Add(Q(cats["equal-intersections"].Id, 1, Difficulty.Medium, now,
            T("Пиёда ўтиш жойида пиёдага йўл бериш мажбуриями?", "Piyoda o'tish joyida piyodaga yo'l berish majburiymi?", "Обязан ли водитель уступить пешеходу на переходе?"),
            T("Ҳа, пиёда ўтиш жойида ҳайдовчи пиёдага йўл бериши шарт.", "Ha, piyoda o'tish joyida haydovchi piyodaga yo'l berishi shart.", "Да, на пешеходном переходе водитель обязан уступить пешеходу."),
            A("Ҳа, мажбурий", "Ha, majburiy", "Да, обязан", true), A("Фақат кундузи", "Faqat kunduzi", "Только днём", false), A("Пиёда кутиши керак", "Piyoda kutishi kerak", "Пешеход должен ждать", false), A("Фақат мактаб олдида", "Faqat maktab oldida", "Только у школы", false)));

        // ── speed-limits: 6 questions ──
        q.Add(Q(cats["speed-limits"].Id, 1, Difficulty.Easy, now,
            T("Аҳоли пунктларида рухсат этилган энг юқори тезлик қанча?", "Aholi punktlarida ruxsat etilgan eng yuqori tezlik qancha?", "Какова максимальная разрешённая скорость в населённых пунктах?"),
            T("60 км/соат, бошқача белгиланмаган бўлса.", "60 km/soat, boshqacha belgilanmagan bo'lsa.", "60 км/ч, если не установлено иное."),
            A("60 км/соат", "60 km/soat", "60 км/ч", true), A("80 км/соат", "80 km/soat", "80 км/ч", false), A("40 км/соат", "40 km/soat", "40 км/ч", false), A("90 км/соат", "90 km/soat", "90 км/ч", false)));

        q.Add(Q(cats["speed-limits"].Id, 2, Difficulty.Medium, now,
            T("Шаҳар ташқарисида енгил автомобил учун рухсат этилган тезлик қанча?", "Shahar tashqarisida yengil avtomobil uchun ruxsat etilgan tezlik qancha?", "Какова разрешённая скорость для легкового автомобиля за городом?"),
            T("100 км/соат автомагистралда, 90 км/соат бошқа йўлларда.", "100 km/soat avtomagistralda, 90 km/soat boshqa yo'llarda.", "100 км/ч на автомагистрали, 90 км/ч на остальных дорогах."),
            A("90–100 км/соат", "90–100 km/soat", "90–100 км/ч", true), A("120 км/соат", "120 km/soat", "120 км/ч", false), A("60 км/соат", "60 km/soat", "60 км/ч", false), A("80 км/соат", "80 km/soat", "80 км/ч", false)));

        q.Add(Q(cats["speed-limits"].Id, 1, Difficulty.Medium, now,
            T("Мактаб ёнидаги тезлик чегараси одатда қанча?", "Maktab yonidagi tezlik chegarasi odatda qancha?", "Каково обычное ограничение скорости у школы?"),
            T("Мактаб ёнида одатда 20–30 км/соат чегара белгиланади.", "Maktab yonida odatda 20–30 km/soat chegara belgilanadi.", "У школы обычно устанавливается ограничение 20–30 км/ч."),
            A("20–30 км/соат", "20–30 km/soat", "20–30 км/ч", true), A("60 км/соат", "60 km/soat", "60 км/ч", false), A("50 км/соат", "50 km/soat", "50 км/ч", false), A("40 км/соат", "40 km/soat", "40 км/ч", false)));

        q.Add(Q(cats["speed-limits"].Id, 3, Difficulty.Easy, now,
            T("Туман пайтида тезликни камайтириш керакми?", "Tuman paytida tezlikni kamaytirish kerakmi?", "Нужно ли снижать скорость в тумане?"),
            T("Ҳа, кўриниш масофаси камайган ҳолатда тезлик пасайтирилиши шарт.", "Ha, ko'rinish masofasi kamaygan holatda tezlik pasaytirilishi shart.", "Да, при уменьшении видимости скорость должна быть снижена."),
            A("Ҳа, мажбурий", "Ha, majburiy", "Да, обязательно", true), A("Йўқ", "Yo'q", "Нет", false), A("Фақат кечаси", "Faqat kechasi", "Только ночью", false), A("Фақат магистралда", "Faqat magistralda", "Только на магистрали", false)));

        q.Add(Q(cats["speed-limits"].Id, 2, Difficulty.Hard, now,
            T("Тезлик чегараси белгиси таъсири қаерда тугайди?", "Tezlik chegarasi belgisi ta'siri qaerda tugaydi?", "Где заканчивается действие знака ограничения скорости?"),
            T("Кейинги чорраҳада ёки тақиқ тугаши белгисида.", "Keyingi chorrahada yoki taqiq tugashi belgisida.", "На следующем перекрёстке или у знака конца ограничения."),
            A("Чорраҳа ёки тугаш белгисида", "Chorraha yoki tugash belgisida", "На перекрёстке или у знака конца", true), A("1 км дан кейин", "1 km dan keyin", "Через 1 км", false), A("Ҳеч қачон тугамайди", "Hech qachon tugamaydi", "Никогда не заканчивается", false), A("Шаҳар чегарасида", "Shahar chegarasida", "На границе города", false)));

        q.Add(Q(cats["speed-limits"].Id, 3, Difficulty.Medium, now,
            T("Тиббий муассаса ёнида тезлик чегараси қанча?", "Tibbiy muassasa yonida tezlik chegarasi qancha?", "Каково ограничение скорости у медицинского учреждения?"),
            T("Белги қўйилган бўлса, белгидаги тезликка риоя қилинади.", "Belgi qo'yilgan bo'lsa, belgidagi tezlikka rioya qilinadi.", "Если установлен знак, соблюдается указанная скорость."),
            A("Белгидаги тезлик", "Belgidagi tezlik", "Скорость на знаке", true), A("20 км/соат", "20 km/soat", "20 км/ч", false), A("Чегара йўқ", "Chegara yo'q", "Нет ограничения", false), A("40 км/соат", "40 km/soat", "40 км/ч", false)));

        // ── first-aid: 6 questions ──
        q.Add(Q(cats["first-aid"].Id, 1, Difficulty.Easy, now,
            T("Йўл ҳодисасида биринчи навбатда нима қилиш керак?", "Yo'l hodisasida birinchi navbatda nima qilish kerak?", "Что нужно сделать в первую очередь при ДТП?"),
            T("Тез ёрдам чақириш ва жабрланганга ёрдам бериш.", "Tez yordam chaqirish va jabrlanganga yordam berish.", "Вызвать скорую помощь и оказать помощь пострадавшему."),
            A("Тез ёрдам чақириш", "Tez yordam chaqirish", "Вызвать скорую", true), A("Жойни тарк этиш", "Joyni tark etish", "Покинуть место", false), A("Фото олиш", "Foto olish", "Сфотографировать", false), A("Машинани кўчириш", "Mashinani ko'chirish", "Переместить машину", false)));

        q.Add(Q(cats["first-aid"].Id, 2, Difficulty.Medium, now,
            T("Қон кетаётганда нима қилиш керак?", "Qon ketayotganda nima qilish kerak?", "Что делать при кровотечении?"),
            T("Яра устига тоза мато қўйиб босиш ва жгут қўйиш.", "Yara ustiga toza mato qo'yib bosish va jgut qo'yish.", "Приложить чистую ткань к ране и наложить жгут."),
            A("Мато босиш ва жгут қўйиш", "Mato bosish va jgut qo'yish", "Прижать ткань и наложить жгут", true), A("Сув қуйиш", "Suv quyish", "Полить водой", false), A("Ҳеч нарса қилмаслик", "Hech narsa qilmaslik", "Ничего не делать", false), A("Дори суриш", "Dori surish", "Нанести лекарство", false)));

        q.Add(Q(cats["first-aid"].Id, 1, Difficulty.Easy, now,
            T("Суяк синганда нима қилиш керак?", "Suyak singanda nima qilish kerak?", "Что делать при переломе кости?"),
            T("Синган жойни ҳаракатлантирмасдан шина қўйиш.", "Singan joyni harakatlantirmasdan shina qo'yish.", "Наложить шину, не перемещая повреждённый участок."),
            A("Шина қўйиш", "Shina qo'yish", "Наложить шину", true), A("Массаж қилиш", "Massaj qilish", "Сделать массаж", false), A("Сувга солиш", "Suvga solish", "Опустить в воду", false), A("Ҳаракатлантириш", "Harakatlantirish", "Перемещать", false)));

        q.Add(Q(cats["first-aid"].Id, 3, Difficulty.Hard, now,
            T("Нафас олиш тўхтаганда биринчи ёрдам?", "Nafas olish to'xtaganda birinchi yordam?", "Первая помощь при остановке дыхания?"),
            T("Сунъий нафас ва юрак массажи бошлаш керак.", "Sun'iy nafas va yurak massaji boshlash kerak.", "Начать искусственное дыхание и непрямой массаж сердца."),
            A("Сунъий нафас ва юрак массажи", "Sun'iy nafas va yurak massaji", "ИВЛ и массаж сердца", true), A("Сув бериш", "Suv berish", "Дать воды", false), A("Кутиш", "Kutish", "Ждать", false), A("Ётқизиш", "Yotqizish", "Уложить", false)));

        q.Add(Q(cats["first-aid"].Id, 2, Difficulty.Easy, now,
            T("Куйганда биринчи ёрдам нима?", "Kuyganda birinchi yordam nima?", "Какова первая помощь при ожоге?"),
            T("Куйган жойни совуқ сув билан совутиш.", "Kuygan joyni sovuq suv bilan sovutish.", "Охладить обожжённое место холодной водой."),
            A("Совуқ сув билан совутиш", "Sovuq suv bilan sovutish", "Охладить холодной водой", true), A("Мой суриш", "Moy surish", "Нанести масло", false), A("Музлатиш", "Muzlatish", "Заморозить", false), A("Боғлаб қўйиш", "Bog'lab qo'yish", "Перевязать", false)));

        q.Add(Q(cats["first-aid"].Id, 3, Difficulty.Medium, now,
            T("Заҳарланишда биринчи ёрдам нима?", "Zaharlanishda birinchi yordam nima?", "Какова первая помощь при отравлении?"),
            T("Тоза ҳавога чиқариш ва тез ёрдам чақириш.", "Toza havoga chiqarish va tez yordam chaqirish.", "Вывести на свежий воздух и вызвать скорую."),
            A("Тоза ҳавога чиқариш", "Toza havoga chiqarish", "Вывести на свежий воздух", true), A("Сув бериш", "Suv berish", "Дать воды", false), A("Ухлатиш", "Uxlatish", "Уложить спать", false), A("Озиқ бериш", "Oziq berish", "Дать еду", false)));

        // ── intersections: 6 questions ──
        q.Add(Q(cats["regulated-intersections"].Id, 1, Difficulty.Medium, now,
            T("Тартибга солинмаган чорраҳада ким биринчи ўтади?", "Tartibga solinmagan chorrahada kim birinchi o'tadi?", "Кто проезжает первым на нерегулируемом перекрёстке?"),
            T("Ўнг томондан келаётган транспорт устунликка эга.", "O'ng tomondan kelayotgan transport ustunlikka ega.", "Приоритет у транспорта, приближающегося справа."),
            A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Справа", true), A("Чапдан келаётган", "Chapdan kelayotgan", "Слева", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Быстрейший", false), A("Каттароқ", "Kattaroq", "Крупнейший", false)));

        q.Add(Q(cats["regulated-intersections"].Id, 2, Difficulty.Hard, now,
            T("Чорраҳада чап буришда кимга йўл бериш керак?", "Chorrahada chap burishda kimga yo'l berish kerak?", "Кому нужно уступить при повороте налево на перекрёстке?"),
            T("Қарши томондан тўғри ва ўнгга ҳаракатланаётган транспортга.", "Qarshi tomondan to'g'ri va o'ngga harakatlanayotgan transportga.", "Встречному транспорту, движущемуся прямо и направо."),
            A("Қарши тўғри ва ўнгга кетаётганга", "Qarshi to'g'ri va o'ngga ketayotganga", "Встречному прямо и направо", true), A("Ҳеч кимга", "Hech kimga", "Никому", false), A("Чапдан келаётганга", "Chapdan kelayotganga", "Приближающемуся слева", false), A("Пиёдаларга", "Piyodalarga", "Пешеходам", false)));

        q.Add(Q(cats["regulated-intersections"].Id, 1, Difficulty.Easy, now,
            T("Чорраҳада тўхташ чизиғи нима учун?", "Chorrahada to'xtash chizig'i nima uchun?", "Для чего стоп-линия на перекрёстке?"),
            T("Тўхташ жойини белгилайди.", "To'xtash joyini belgilaydi.", "Указывает место остановки."),
            A("Тўхташ жойини белгилайди", "To'xtash joyini belgilaydi", "Обозначает место остановки", true), A("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости", false), A("Пиёда ўтиш жойи", "Piyoda o'tish joyi", "Пешеходный переход", false), A("Парковка жойи", "Parkovka joyi", "Место парковки", false)));

        q.Add(Q(cats["regulated-intersections"].Id, 3, Difficulty.Medium, now,
            T("Т-шаклидаги чорраҳада ким устун?", "T-shaklidagi chorrahada kim ustun?", "Кто имеет приоритет на Т-образном перекрёстке?"),
            T("Асосий йўлдаги транспорт устунликка эга.", "Asosiy yo'ldagi transport ustunlikka ega.", "Приоритет у транспорта на главной дороге."),
            A("Асосий йўлдаги транспорт", "Asosiy yo'ldagi transport", "Транспорт на главной", true), A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Справа", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Быстрейший", false), A("Чапдан келаётган", "Chapdan kelayotgan", "Слева", false)));

        q.Add(Q(cats["regulated-intersections"].Id, 2, Difficulty.Easy, now,
            T("Чорраҳада трамвай устунликка эгами?", "Chorrahada tramvay ustunlikka egami?", "Имеет ли трамвай приоритет на перекрёстке?"),
            T("Тенг шароитда трамвай устунликка эга.", "Teng sharoitda tramvay ustunlikka ega.", "При равных условиях трамвай имеет приоритет."),
            A("Ҳа, тенг шароитда", "Ha, teng sharoitda", "Да, при равных условиях", true), A("Йўқ", "Yo'q", "Нет", false), A("Фақат светофорда", "Faqat svetoforda", "Только на светофоре", false), A("Фақат кечаси", "Faqat kechasi", "Только ночью", false)));

        q.Add(Q(cats["regulated-intersections"].Id, 3, Difficulty.Hard, now,
            T("Чорраҳага кирган, лекин тиқилинч туфайли ўта олмаётган ҳайдовчи нима қилиши керак?", "Chorrahaga kirgan, lekin tiqilinch tufayli o'ta olmayotgan haydovchi nima qilishi kerak?", "Что делать водителю, выехавшему на перекрёсток, но не имеющему возможности проехать из-за затора?"),
            T("Чорраҳага кирмаслик керак эди, тиқилинч бўлса.", "Chorrahaga kirmaslik kerak edi, tiqilinch bo'lsa.", "Не следовало въезжать на перекрёсток при заторе."),
            A("Чорраҳага кирмаслик керак эди", "Chorrahaga kirmaslik kerak edi", "Не следовало въезжать", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Кутиш", "Kutish", "Ждать", false), A("Орқага юриш", "Orqaga yurish", "Сдать назад", false)));

        // ── pedestrians: 6 questions ──
        q.Add(Q(cats["participant-duties"].Id, 1, Difficulty.Easy, now,
            T("Пиёда ўтиш жойида ҳайдовчи нима қилиши керак?", "Piyoda o'tish joyida haydovchi nima qilishi kerak?", "Что должен делать водитель на пешеходном переходе?"),
            T("Тезликни камайтириш ва пиёдаларга йўл бериш.", "Tezlikni kamaytirish va piyodalarga yo'l berish.", "Снизить скорость и уступить дорогу пешеходам."),
            A("Тезликни камайтириш ва йўл бериш", "Tezlikni kamaytirish va yo'l berish", "Снизить скорость и уступить", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Тезлатиш", "Tezlatish", "Ускориться", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["participant-duties"].Id, 2, Difficulty.Medium, now,
            T("Кўзи ожиз пиёдага қандай муносабатда бўлиш керак?", "Ko'zi ojiz piyodaga qanday munosabatda bo'lish kerak?", "Как относиться к слепому пешеходу?"),
            T("Оқ таёқчали пиёда учраганда доимо йўл бериш мажбурий.", "Oq tayoqchali piyoda uchraganda doimo yo'l berish majburiy.", "При встрече пешехода с белой тростью обязательно уступить дорогу."),
            A("Доимо йўл бериш", "Doimo yo'l berish", "Всегда уступить", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Четлаб ўтиш", "Chetlab o'tish", "Объехать", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["participant-duties"].Id, 3, Difficulty.Easy, now,
            T("Болалар ўтиш жойи олдида нима қилиш керак?", "Bolalar o'tish joyi oldida nima qilish kerak?", "Что делать перед детским переходом?"),
            T("Тезликни камайтириш ва болаларга йўл бериш.", "Tezlikni kamaytirish va bolalarga yo'l berish.", "Снизить скорость и уступить дорогу детям."),
            A("Тезликни камайтириш", "Tezlikni kamaytirish", "Снизить скорость", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Тез ўтиб кетиш", "Tez o'tib ketish", "Быстро проехать", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["participant-duties"].Id, 1, Difficulty.Medium, now,
            T("Пиёда ўтиш жойида қувиб ўтиш мумкинми?", "Piyoda o'tish joyida quvib o'tish mumkinmi?", "Разрешён ли обгон на пешеходном переходе?"),
            T("Йўқ, пиёда ўтиш жойида қувиб ўтиш тақиқланади.", "Yo'q, piyoda o'tish joyida quvib o'tish taqiqlanadi.", "Нет, обгон на пешеходном переходе запрещён."),
            A("Тақиқланади", "Taqiqlanadi", "Запрещён", true), A("Рухсат", "Ruxsat", "Разрешён", false), A("Фақат кечаси", "Faqat kechasi", "Только ночью", false), A("Секин бўлса рухсат", "Sekin bo'lsa ruxsat", "Разрешён при низкой скорости", false)));

        q.Add(Q(cats["participant-duties"].Id, 2, Difficulty.Hard, now,
            T("Пиёдалар йўлнинг қайси томонида юриши керак?", "Piyodalar yo'lning qaysi tomonida yurishi kerak?", "По какой стороне дороги должны идти пешеходы?"),
            T("Транспортга қарши — чап томонда юриш тавсия этилади.", "Transportga qarshi — chap tomonda yurish tavsiya etiladi.", "Навстречу транспорту — рекомендуется идти по левой стороне."),
            A("Транспортга қарши", "Transportga qarshi", "Навстречу транспорту", true), A("Транспорт билан бир томонда", "Transport bilan bir tomonda", "По ходу транспорта", false), A("Йўл ўртасида", "Yo'l o'rtasida", "По центру дороги", false), A("Ихтиёрий", "Ixtiyoriy", "Произвольно", false)));

        q.Add(Q(cats["participant-duties"].Id, 3, Difficulty.Medium, now,
            T("Тунда пиёда нима тақиши керак?", "Tunda piyoda nima taqishi kerak?", "Что должен носить пешеход ночью?"),
            T("Акс эттирувчи элементлар ёки ёруғ кийим тақиш тавсия этилади.", "Aks ettiruvchi elementlar yoki yorug' kiyim taqish tavsiya etiladi.", "Рекомендуется носить светоотражающие элементы или яркую одежду."),
            A("Акс эттирувчи элементлар", "Aks ettiruvchi elementlar", "Светоотражающие элементы", true), A("Қора кийим", "Qora kiyim", "Тёмная одежда", false), A("Фонарь", "Fonar'", "Фонарь", false), A("Ҳеч нарса", "Hech narsa", "Ничего", false)));

        // ── parking: 4 questions ──
        q.Add(Q(cats["parking"].Id, 1, Difficulty.Easy, now,
            T("Тўхташ қаерда тақиқланган?", "To'xtash qaerda taqiqlangan?", "Где запрещена остановка?"),
            T("Пиёда ўтиш жойида, чорраҳаларда ва кўприкларда.", "Piyoda o'tish joyida, chorrahalarda va ko'priklarda.", "На пешеходных переходах, перекрёстках и мостах."),
            A("Пиёда ўтиш жойи, чорраҳа, кўприк", "Piyoda o'tish joyi, chorraha, ko'prik", "Переход, перекрёсток, мост", true), A("Ҳамма жойда", "Hamma joyda", "Везде", false), A("Фақат шаҳарда", "Faqat shaharda", "Только в городе", false), A("Ҳеч қаерда", "Hech qaerda", "Нигде", false)));

        q.Add(Q(cats["parking"].Id, 2, Difficulty.Medium, now,
            T("Стоянка тақиқланган белги нимани англатади?", "Stoyanka taqiqlangan belgi nimani anglatadi?", "Что означает знак «Стоянка запрещена»?"),
            T("5 дақиқадан ортиқ тўхташ тақиқланади.", "5 daqiqadan ortiq to'xtash taqiqlanadi.", "Остановка более чем на 5 минут запрещена."),
            A("5 дақиқадан ортиқ тўхташ тақиқланади", "5 daqiqadan ortiq to'xtash taqiqlanadi", "Стоянка более 5 минут запрещена", true), A("Тўхташ тақиқланади", "To'xtash taqiqlanadi", "Остановка запрещена", false), A("Парковка пуллик", "Parkovka pullik", "Парковка платная", false), A("Фақат кечаси рухсат", "Faqat kechasi ruxsat", "Только ночью разрешена", false)));

        q.Add(Q(cats["parking"].Id, 3, Difficulty.Easy, now,
            T("Қизил-кўк тақиқланган белги қаерда ўрнатилади?", "Qizil-ko'k taqiqlangan belgi qaerda o'rnatiladi?", "Где устанавливается красно-синий запрещающий знак?"),
            T("Тўхташ ва стоянка тақиқланган жойларда.", "To'xtash va stoyanka taqiqlangan joylarda.", "В местах, где запрещены остановка и стоянка."),
            A("Тўхташ тақиқланган жойда", "To'xtash taqiqlangan joyda", "Где запрещена остановка", true), A("Парковка жойида", "Parkovka joyida", "На парковке", false), A("Бекатларда", "Bekatlarda", "На остановках", false), A("Шаҳар четида", "Shahar chetida", "На окраине города", false)));

        q.Add(Q(cats["parking"].Id, 2, Difficulty.Hard, now,
            T("Автобус бекати олдида неча метрда тўхташ мумкин эмас?", "Avtobus bekati oldida necha metrda to'xtash mumkin emas?", "На каком расстоянии от автобусной остановки нельзя останавливаться?"),
            T("15 метр масофада тўхташ тақиқланади.", "15 metr masofada to'xtash taqiqlanadi.", "Остановка запрещена в 15 метрах."),
            A("15 метр", "15 metr", "15 метров", true), A("5 метр", "5 metr", "5 метров", false), A("10 метр", "10 metr", "10 метров", false), A("30 метр", "30 metr", "30 метров", false)));

        // ── road-markings: 4 questions ──
        q.Add(Q(cats["road-markings"].Id, 1, Difficulty.Easy, now,
            T("Узлуксиз чизиқ нимани англатади?", "Uzluksiz chiziq nimani anglatadi?", "Что означает сплошная линия?"),
            T("Кесиб ўтиш тақиқланади.", "Kesib o'tish taqiqlanadi.", "Пересечение запрещено."),
            A("Кесиб ўтиш тақиқланади", "Kesib o'tish taqiqlanadi", "Пересечение запрещено", true), A("Кесиб ўтиш мумкин", "Kesib o'tish mumkin", "Пересечение разрешено", false), A("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости", false), A("Парковка жойи", "Parkovka joyi", "Место парковки", false)));

        q.Add(Q(cats["road-markings"].Id, 2, Difficulty.Medium, now,
            T("Узуқ чизиқ нимани англатади?", "Uzuq chiziq nimani anglatadi?", "Что означает прерывистая линия?"),
            T("Қатор алмаштириш ва қувиб ўтиш рухсат этилади.", "Qator almashtirish va quvib o'tish ruxsat etiladi.", "Разрешена смена полосы и обгон."),
            A("Қатор алмаштириш рухсат", "Qator almashtirish ruxsat", "Смена полосы разрешена", true), A("Тўхташ жойи", "To'xtash joyi", "Место остановки", false), A("Тезликни оширинг", "Tezlikni oshiring", "Увеличьте скорость", false), A("Кесиб ўтиш тақиқ", "Kesib o'tish taqiq", "Пересечение запрещено", false)));

        q.Add(Q(cats["road-markings"].Id, 3, Difficulty.Hard, now,
            T("Сариқ зигзаг чизиғи нимани англатади?", "Sariq zigzag chizig'i nimani anglatadi?", "Что означает жёлтая зигзагообразная линия?"),
            T("Жамоат транспорти бекати — тўхташ тақиқланган.", "Jamoat transporti bekati — to'xtash taqiqlangan.", "Остановка общественного транспорта — остановка запрещена."),
            A("Жамоат транспорти бекати", "Jamoat transporti bekati", "Остановка общ. транспорта", true), A("Парковка жойи", "Parkovka joyi", "Парковка", false), A("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости", false), A("Пиёда ўтиш жойи", "Piyoda o'tish joyi", "Пешеходный переход", false)));

        q.Add(Q(cats["road-markings"].Id, 1, Difficulty.Medium, now,
            T("Қўш узлуксиз чизиқ нимани англатади?", "Qo'sh uzluksiz chiziq nimani anglatadi?", "Что означает двойная сплошная линия?"),
            T("Қарши йўналишдаги оқимларни ажратади, кесиб ўтиш қатъиян тақиқ.", "Qarshi yo'nalishdagi oqimlarni ajratadi, kesib o'tish qat'iyan taqiq.", "Разделяет встречные потоки, пересечение строго запрещено."),
            A("Кесиб ўтиш қатъиян тақиқ", "Kesib o'tish qat'iyan taqiq", "Пересечение строго запрещено", true), A("Кесиб ўтиш мумкин", "Kesib o'tish mumkin", "Пересечение разрешено", false), A("Тўхташ жойи", "To'xtash joyi", "Место остановки", false), A("Велосипед йўлакчаси", "Velosiped yo'lakchasi", "Велодорожка", false)));

        // Assign ticket numbers: distribute 60 questions across tickets 1-3 (20 each)
        for (var i = 0; i < q.Count; i++)
            q[i].TicketNumber = (i / 20) + 1;

        db.Questions.AddRange(q);
        logger.LogInformation("Seeded {Count} questions with answer options", q.Count);
    }

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, ct))
            return;

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "998901234567",
            FirstName = "Admin",
            LastName = "Avtolider",
            Role = UserRole.Admin,
            AuthProvider = AuthProvider.Phone,
            PreferredLanguage = Language.UzLatin,
            IsBlocked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    // ── Phase 2 Content Seeding ──

    private async Task SeedTrafficFinesAsync(CancellationToken ct)
    {
        if (await db.TrafficFines.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        var fines = new List<TrafficFine>
        {
            Fine("12.1", T("Тезликни 20-40 км/с ошириш", "Tezlikni 20-40 km/s oshirish", "Превышение скорости на 20-40 км/ч"), 300_000_00, null, 1, now),
            Fine("12.2", T("Тезликни 40-60 км/с ошириш", "Tezlikni 40-60 km/s oshirish", "Превышение скорости на 40-60 км/ч"), 600_000_00, null, 2, now),
            Fine("12.3", T("Тезликни 60 км/с дан ортиқ ошириш", "Tezlikni 60 km/s dan ortiq oshirish", "Превышение скорости более чем на 60 км/ч"), 1_000_000_00, null, 3, now),
            Fine("13.1", T("Қизил чироқда ўтиш", "Qizil chiroqda o'tish", "Проезд на красный сигнал светофора"), 600_000_00, null, 4, now),
            Fine("14.1", T("Хавфсизлик камарини тақмаслик", "Xavfsizlik kamarini taqmaslik", "Неиспользование ремня безопасности"), 150_000_00, null, 5, now),
            Fine("14.2", T("Мотоциклда дубулға киймаслик", "Mototsiklda dubulg'a kiymaslik", "Управление мотоциклом без шлема"), 150_000_00, null, 6, now),
            Fine("15.1", T("Ҳайдовчилик гувоҳномасиз бошқариш", "Haydovchilik guvohnomasiz boshqarish", "Управление ТС без водительского удостоверения"), 1_500_000_00, null, 7, now),
            Fine("15.2", T("Маст ҳолда бошқариш", "Mast holda boshqarish", "Управление ТС в нетрезвом состоянии"), 3_000_000_00, null, 8, now),
            Fine("16.1", T("Пиёдалар ўтиш жойида тўхтамаслик", "Piyodalar o'tish joyida to'xtamaslik", "Не уступил дорогу пешеходу на переходе"), 400_000_00, null, 9, now),
            Fine("16.2", T("Тўхташ тақиқланган жойда тўхташ", "To'xtash taqiqlangan joyda to'xtash", "Остановка в запрещённом месте"), 200_000_00, null, 10, now),
            Fine("17.1", T("Қарши йўналишда ҳаракатланиш", "Qarshi yo'nalishda harakatlanish", "Движение по встречной полосе"), 800_000_00, null, 11, now),
            Fine("17.2", T("Тротуарда ҳаракатланиш", "Trotuarda harakatlanish", "Движение по тротуару"), 300_000_00, null, 12, now),
            Fine("18.1", T("Қўл телефонида гапириб бошқариш", "Qo'l telefonida gapirib boshqarish", "Разговор по телефону во время управления ТС"), 300_000_00, null, 13, now),
            Fine("18.2", T("Ойнасига пардоз ёпиштирилган автомобилни бошқариш", "Oynasiga pardoz yopishtirilgan avtomobilni boshqarish", "Управление ТС с тонированными стёклами"), 300_000_00, null, 14, now),
            Fine("19.1", T("Давлат рақам белгисиз ҳаракатланиш", "Davlat raqam belgisiz harakatlanish", "Управление ТС без госномеров"), 500_000_00, null, 15, now),
            Fine("19.2", T("Йўл ҳаракати қоидаларини бузиб ўтиш натижасида авария содир этиш", "Yo'l harakati qoidalarini buzib o'tish natijasida avariya sodir etish", "ДТП с причинением материального ущерба"), 1_000_000_00, 3_000_000_00, 16, now),
            Fine("20.1", T("Суғурта полисисиз бошқариш", "Sug'urta polisisiz boshqarish", "Управление ТС без страхового полиса"), 300_000_00, null, 17, now),
            Fine("20.2", T("Техник кўрикдан ўтмаган автомобилни бошқариш", "Texnik ko'rikdan o'tmagan avtomobilni boshqarish", "Управление ТС без прохождения техосмотра"), 300_000_00, null, 18, now),
            Fine("21.1", T("Белгиланмаган жойда йўлни кесиб ўтиш (пиёда)", "Belgilanmagan joyda yo'lni kesib o'tish (piyoda)", "Переход дороги в неустановленном месте (пешеход)"), 50_000_00, null, 19, now),
            Fine("21.2", T("Тоқтаб қолган транспорт ёнидан ўтишда эҳтиёт бўлмаслик", "Toqtab qolgan transport yonidan o'tishda ehtiyot bo'lmaslik", "Нарушение правил обгона"), 600_000_00, null, 20, now),
        };

        db.TrafficFines.AddRange(fines);
        logger.LogInformation("Seeded {Count} traffic fines", fines.Count);
    }

    private async Task SeedHazardLabelsAsync(CancellationToken ct)
    {
        if (await db.HazardLabels.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;
        var labels = new List<HazardLabel>
        {
            Hazard("explosive", "GHS01", T("Портловчи моддалар", "Portlovchi moddalar", "Взрывчатые вещества"),
                T("Портлаш хавфи мавжуд бўлган моддалар ва аралашмалар", "Portlash xavfi mavjud bo'lgan moddalar va aralashmalar", "Вещества и смеси, представляющие опасность взрыва"), 1, now, "questions/visual/hazard/221ba9efeaad4805b52c43ff4ab3411e.webp"),
            Hazard("flammable-gas", "GHS02", T("Ёнувчи газлар", "Yonuvchi gazlar", "Воспламеняющиеся газы"),
                T("Ҳаво билан аралашганда ёнувчи аралашма ҳосил қиладиган газлар", "Havo bilan aralashganda yonuvchi aralashma hosil qiladigan gazlar", "Газы, образующие воспламеняющиеся смеси с воздухом"), 2, now, "questions/visual/hazard/7f9a94bd95de4d4f8ad3f7c94349cb87.webp"),
            Hazard("flammable-aerosol", "GHS02a", T("Ёнувчи аэрозоллар", "Yonuvchi aerozollar", "Воспламеняющиеся аэрозоли"),
                T("Ёнувчи компонентлари бор аэрозоль идишлар", "Yonuvchi komponentlari bor aerozol idishlar", "Аэрозольные упаковки с воспламеняющимися компонентами"), 3, now, "questions/visual/hazard/79d41157779b45aa805755ae94fb1c2b.webp"),
            Hazard("oxidizing-gas", "GHS03", T("Оксидловчи газлар", "Oksidlovchi gazlar", "Окисляющие газы"),
                T("Бошқа моддаларнинг ёнишига сабаб бўладиган газлар", "Boshqa moddalarning yonishiga sabab bo'ladigan gazlar", "Газы, способствующие горению других веществ"), 4, now, "questions/visual/hazard/3b1256de6b734cb2b055df996d621125.webp"),
            Hazard("gas-under-pressure", "GHS04", T("Босим остидаги газлар", "Bosim ostidagi gazlar", "Газы под давлением"),
                T("Юқори босим остида сақланаётган газлар", "Yuqori bosim ostida saqlanayotgan gazlar", "Газы, хранящиеся под высоким давлением"), 5, now, "questions/visual/hazard/b3174bb02ba5482e92e89d5e30f49fec.webp"),
            Hazard("flammable-liquid", "GHS02b", T("Ёнувчи суюқликлар", "Yonuvchi suyuqliklar", "Воспламеняющиеся жидкости"),
                T("Паст ёниш нуқтасига эга бўлган суюқликлар", "Past yonish nuqtasiga ega bo'lgan suyuqliklar", "Жидкости с низкой температурой вспышки"), 6, now, "questions/visual/hazard/dcc3b8dc02fa4568b0503699be79e128.webp"),
            Hazard("flammable-solid", "GHS02c", T("Ёнувчи қаттиқ моддалар", "Yonuvchi qattiq moddalar", "Воспламеняющиеся твёрдые вещества"),
                T("Ишқаланиш ёки қисқа муддатли тутантиришдан ёнадиган қаттиқ моддалар", "Ishqalanish yoki qisqa muddatli tutantirishdan yonadigan qattiq moddalar", "Твёрдые вещества, воспламеняющиеся от трения или кратковременного воздействия огня"), 7, now, "questions/visual/hazard/ef5111a5a08844909c89b0986d877287.webp"),
            Hazard("self-reactive", "GHS01a", T("Ўз-ўзидан реакцияга кирадиган", "O'z-o'zidan reaksiyaga kiradigan", "Самореактивные вещества"),
                T("Термик ностабил бўлган ва ташқи ёниш манбаисиз парчаланиши мумкин", "Termik nostabil bo'lgan va tashqi yonish manba'isiz parchalanishi mumkin", "Термически нестабильные вещества, способные к разложению без внешнего источника огня"), 8, now, "questions/visual/hazard/1fea4869b9c94b1fb16b3a2814640b07.webp"),
            Hazard("pyrophoric", "GHS02d", T("Пирофорик моддалар", "Piroforik moddalar", "Пирофорные вещества"),
                T("Ҳаво билан алоқада ўз-ўзидан ёнадиган моддалар", "Havo bilan aloqada o'z-o'zidan yonadigan moddalar", "Вещества, самовоспламеняющиеся при контакте с воздухом"), 9, now, "questions/visual/hazard/8c86f09e89274bbea298e809476dd919.webp"),
            Hazard("self-heating", "GHS02e", T("Ўз-ўзидан қизийдиган", "O'z-o'zidan qiziydigan", "Самонагревающиеся вещества"),
                T("Ташқи энергия манбаисиз ўз-ўзидан қизиб кетадиган моддалар", "Tashqi energiya manba'isiz o'z-o'zidan qizib ketadigan moddalar", "Вещества, способные самонагреваться без внешнего источника энергии"), 10, now, "questions/visual/hazard/988e37aa023944bd8d1ef4ae8a982070.webp"),
            Hazard("water-reactive", "GHS02f", T("Сув билан реакцияга кирадиган", "Suv bilan reaksiyaga kiradigan", "Реагирующие с водой"),
                T("Сув билан алоқада ёнувчи газ ажратадиган моддалар", "Suv bilan aloqada yonuvchi gaz ajratadigan moddalar", "Вещества, выделяющие воспламеняющиеся газы при контакте с водой"), 11, now, "questions/visual/hazard/3b8992365d8043eebf3e017768175d89.webp"),
            Hazard("oxidizer", "GHS03a", T("Оксидловчи моддалар", "Oksidlovchi moddalar", "Окислители"),
                T("Бошқа моддаларнинг ёнишини тезлаштирадиган моддалар", "Boshqa moddalarning yonishini tezlashtiradigan moddalar", "Вещества, способствующие воспламенению или усиливающие горение"), 12, now, "questions/visual/hazard/45c3d65e481f4fe39117a7e05f1993bd.webp"),
            Hazard("organic-peroxide", "GHS01b", T("Органик пероксидлар", "Organik peroksidlar", "Органические пероксиды"),
                T("Ёниш ва портлаш хавфи бор органик моддалар", "Yonish va portlash xavfi bor organik moddalar", "Органические вещества с опасностью возгорания и взрыва"), 13, now, "questions/visual/hazard/85be6a33af3d4e1fbcf50c84766cfdac.webp"),
            Hazard("toxic", "GHS06", T("Заҳарли моддалар", "Zaharli moddalar", "Токсичные вещества"),
                T("Кам миқдорда ҳам инсон саломатлигига жиддий хавф туғдирадиган моддалар", "Kam miqdorda ham inson salomatligiga jiddiy xavf tug'diradigan moddalar", "Вещества, представляющие серьёзную опасность для здоровья даже в малых дозах"), 14, now, "questions/visual/hazard/fdf617dd2d1d428abf97e6e442e42e2a.webp"),
            Hazard("irritant", "GHS07", T("Зарарли моддалар", "Zararli moddalar", "Вредные вещества"),
                T("Тери ва кўзни таъсирлантирувчи моддалар", "Teri va ko'zni ta'sirlantiruuvchi moddalar", "Вещества, раздражающие кожу и глаза"), 15, now, "questions/visual/hazard/77fbbda129cf4d76b2a09bc046782608.webp"),
            Hazard("corrosive", "GHS05", T("Емирувчи моддалар", "Yemiruvchi moddalar", "Коррозионные вещества"),
                T("Тери, кўз ва металларни емирадиган моддалар", "Teri, ko'z va metallarni yemiradigan moddalar", "Вещества, разрушающие кожу, глаза и металлы"), 16, now, "questions/visual/hazard/7b8f7c7438374d61837c8974779223c0.webp"),
            Hazard("health-hazard", "GHS08", T("Соғлиққа хавфли", "Sog'liqqa xavfli", "Опасность для здоровья"),
                T("Узоқ муддатли таъсирда саратон ёки бошқа оғир касалликларга олиб келадиган моддалар", "Uzoq muddatli ta'sirda saraton yoki boshqa og'ir kasalliklarga olib keladigan moddalar", "Вещества, вызывающие рак или другие тяжёлые заболевания при длительном воздействии"), 17, now, "questions/visual/hazard/9ad2aca17fdb4eca8080dc1a5cdbbd0d.webp"),
            Hazard("environmental-hazard", "GHS09", T("Атроф-муҳитга хавфли", "Atrof-muhitga xavfli", "Опасность для окружающей среды"),
                T("Сув организмларига заҳарли бўлган моддалар", "Suv organizmlariga zaharli bo'lgan moddalar", "Вещества, токсичные для водных организмов"), 18, now, "questions/visual/hazard/2b533aec59604788ba714eebc8a05f66.webp"),
        };

        db.HazardLabels.AddRange(labels);
        logger.LogInformation("Seeded {Count} hazard labels", labels.Count);
    }

    private async Task SeedFirstAidProceduresAsync(CancellationToken ct)
    {
        if (await db.FirstAidProcedures.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;

        var cpr = MakeProcedure("cpr", T("Юрак-ўпка реанимацияси", "Yurak-o'pka reanimatsiyasi", "Сердечно-лёгочная реанимация"),
            T("Нафас олиш ва юрак уриши тўхтаганда қўлланиладиган усул", "Nafas olish va yurak urishi to'xtaganda qo'llaniladigan usul", "Метод, применяемый при остановке дыхания и сердцебиения"), 1, now,
            [
                Step(T("Хавфсизликни текшириш", "Xavfsizlikni tekshirish", "Проверка безопасности"),
                    T("Жабрланувчи ётган жой хавфсиз эканлигига ишонч ҳосил қилинг", "Jabrlanuvchi yotgan joy xavfsiz ekanligiga ishonch hosil qiling", "Убедитесь, что место, где лежит пострадавший, безопасно"), 1),
                Step(T("Ҳушини текшириш", "Hushini tekshirish", "Проверка сознания"),
                    T("Жабрланувчининг елкасига секин уриб чақиринг", "Jabrlanuvchining yelkasiga sekin urib chaqiring", "Аккуратно потрясите пострадавшего за плечо и позовите"), 2),
                Step(T("Тез ёрдамни чақириш", "Tez yordamni chaqirish", "Вызов скорой помощи"),
                    T("103 рақамига қўнғироқ қилинг", "103 raqamiga qo'ng'iroq qiling", "Позвоните по номеру 103"), 3),
                Step(T("Кўкрак бўшлиғига босиш", "Ko'krak bo'shlig'iga bosish", "Компрессии грудной клетки"),
                    T("Кўкрак суягининг пастки учидан 2 бармоқ юқорига 30 марта босинг", "Ko'krak suyagining pastki uchidan 2 barmoq yuqoriga 30 marta bosing", "Нажимайте на грудину на 2 пальца выше нижнего края — 30 нажатий"), 4),
                Step(T("Сунъий нафас", "Sun'iy nafas", "Искусственное дыхание"),
                    T("Бошни орқага ташлаб 2 марта оғизга нафас беринг", "Boshni orqaga tashlab 2 marta og'izga nafas bering", "Запрокиньте голову назад и сделайте 2 вдоха рот в рот"), 5),
                Step(T("Давом эттириш", "Davom ettirish", "Продолжение"),
                    T("30:2 нисбатда тез ёрдам келгунча давом эттиринг", "30:2 nisbatda tez yordam kelguncha davom ettiring", "Продолжайте в соотношении 30:2 до приезда скорой помощи"), 6),
            ]);

        var wound = MakeProcedure("wound-treatment", T("Яраларни даволаш", "Yaralarni davolash", "Обработка ран"),
            T("Ташқи жароҳатларни дастлабки даволаш усуллари", "Tashqi jarohatlarni dastlabki davolash usullari", "Методы первичной обработки наружных ран"), 2, now,
            [
                Step(T("Қўлларни ювиш", "Qo'llarni yuvish", "Мытьё рук"),
                    T("Ярани даволашдан олдин қўлларингизни совунлаб ювинг", "Yarani davolashdan oldin qo'llaringizni sovunlab yuving", "Перед обработкой раны тщательно вымойте руки с мылом"), 1),
                Step(T("Қон оқишини тўхтатиш", "Qon oqishini to'xtatish", "Остановка кровотечения"),
                    T("Тоза мато билан ярага босиб ушланг", "Toza mato bilan yaraga bosib ushlang", "Прижмите рану чистой тканью"), 2),
                Step(T("Ярани ювиш", "Yarani yuvish", "Промывание раны"),
                    T("Ярани оқар сув остида ювинг", "Yarani oqar suv ostida yuving", "Промойте рану под проточной водой"), 3),
                Step(T("Антисептик суриш", "Antiseptik surish", "Обработка антисептиком"),
                    T("Яра атрофига антисептик суринг", "Yara atrofiga antiseptik suring", "Обработайте кожу вокруг раны антисептиком"), 4),
                Step(T("Боғлам қўйиш", "Bog'lam qo'yish", "Наложение повязки"),
                    T("Тоза боғлам билан ярани боғланг", "Toza bog'lam bilan yarani bog'lang", "Наложите на рану чистую повязку"), 5),
            ]);

        var fracture = MakeProcedure("fracture-care", T("Синиш ҳолатлари", "Sinish holatlari", "Помощь при переломах"),
            T("Суяк синишида биринчи ёрдам кўрсатиш", "Suyak sinishida birinchi yordam ko'rsatish", "Первая помощь при переломах костей"), 3, now,
            [
                Step(T("Жабрланувчини тинчлантириш", "Jabrlanuvchini tinchlantirish", "Успокоить пострадавшего"),
                    T("Жабрланувчини ҳаракатлантирманг ва тинчлантиринг", "Jabrlanuvchini harakatlantirmang va tinchlantiring", "Не перемещайте пострадавшего и успокойте его"), 1),
                Step(T("Шина қўйиш", "Shina qo'yish", "Наложение шины"),
                    T("Синган жойни қўшни бўғимлар билан бирга шинага маҳкамланг", "Singan joyni qo'shni bo'g'imlar bilan birga shinaga mahkamlang", "Зафиксируйте место перелома шиной вместе с соседними суставами"), 2),
                Step(T("Совуқ қўйиш", "Sovuq qo'yish", "Приложение холода"),
                    T("Шишишни камайтириш учун муз қўйинг", "Shishishni kamaytirish uchun muz qo'ying", "Приложите лёд для уменьшения отёка"), 3),
                Step(T("Оғриқ қолдирувчи бериш", "Og'riq qoldiruvchi berish", "Обезболивание"),
                    T("Имконият бўлса оғриқ қолдирувчи дори беринг", "Imkoniyat bo'lsa og'riq qoldiruvchi dori bering", "При возможности дайте обезболивающее"), 4),
                Step(T("Касалхонага етказиш", "Kasalxonaga yetkazish", "Доставка в больницу"),
                    T("Тез ёрдамни чақиринг ёки касалхонага олиб боринг", "Tez yordamni chaqiring yoki kasalxonaga olib boring", "Вызовите скорую или доставьте в больницу"), 5),
            ]);

        var burns = MakeProcedure("burns", T("Куйиш ҳолатлари", "Kuyish holatlari", "Помощь при ожогах"),
            T("Куйиш жароҳатларида биринчи ёрдам", "Kuyish jarohatlarida birinchi yordam", "Первая помощь при ожогах"), 4, now,
            [
                Step(T("Куйиш манбаидан узоқлаштириш", "Kuyish manba'idan uzoqlashtirish", "Удаление от источника ожога"),
                    T("Жабрланувчини иссиқлик манбаидан узоқлаштиринг", "Jabrlanuvchini issiqlik manba'idan uzoqlashtiring", "Удалите пострадавшего от источника тепла"), 1),
                Step(T("Совуқ сув билан совутиш", "Sovuq suv bilan sovutish", "Охлаждение водой"),
                    T("Куйган жойни 10-20 дақиқа совуқ сув остида ушланг", "Kuygan joyni 10-20 daqiqa sovuq suv ostida ushlang", "Держите обожжённое место под прохладной водой 10-20 минут"), 2),
                Step(T("Кийимларни эҳтиёт қилиб олиш", "Kiyimlarni ehtiyot qilib olish", "Аккуратное снятие одежды"),
                    T("Ёпишмаган кийимларни эҳтиёт билан олинг", "Yopishmagan kiyimlarni ehtiyot bilan oling", "Аккуратно снимите одежду, если она не прилипла"), 3),
                Step(T("Стерил боғлам қўйиш", "Steril bog'lam qo'yish", "Стерильная повязка"),
                    T("Куйган жойга стерил боғлам қўйинг", "Kuygan joyga steril bog'lam qo'ying", "Наложите стерильную повязку на ожог"), 4),
            ]);

        var bleeding = MakeProcedure("severe-bleeding", T("Кучли қон кетиш", "Kuchli qon ketish", "Сильное кровотечение"),
            T("Кучли қон кетишни тўхтатиш усуллари", "Kuchli qon ketishni to'xtatish usullari", "Методы остановки сильного кровотечения"), 5, now,
            [
                Step(T("Тўғридан-тўғри босим", "To'g'ridan-to'g'ri bosim", "Прямое давление"),
                    T("Тоза мато билан ярага маҳкам босинг", "Toza mato bilan yaraga mahkam bosing", "Крепко прижмите к ране чистую ткань"), 1),
                Step(T("Оёқ/қўлни кўтариш", "Oyoq/qo'lni ko'tarish", "Поднятие конечности"),
                    T("Жароҳатланган оёқ/қўлни юрак сатҳидан юқори кўтаринг", "Jarohatulangan oyoq/qo'lni yurak sathidan yuqori ko'taring", "Поднимите повреждённую конечность выше уровня сердца"), 2),
                Step(T("Жгут қўйиш", "Jgut qo'yish", "Наложение жгута"),
                    T("Агар қон тўхтамаса, жгутни жароҳатдан юқорига қўйинг", "Agar qon to'xtamasa, jgutni jarohatdan yuqoriga qo'ying", "Если кровотечение не останавливается, наложите жгут выше раны"), 3),
                Step(T("Жгут вақтини ёзиш", "Jgut vaqtini yozish", "Запись времени жгута"),
                    T("Жгут қўйилган вақтни ёзиб қўйинг", "Jgut qo'yilgan vaqtni yozib qo'ying", "Запишите время наложения жгута"), 4),
                Step(T("Тез ёрдамни кутиш", "Tez yordamni kutish", "Ожидание скорой"),
                    T("Жабрланувчини иссиқ тутинг ва тез ёрдамни кутинг", "Jabrlanuvchini issiq tuting va tez yordamni kuting", "Согрейте пострадавшего и ждите скорую помощь"), 5),
            ]);

        var choking = MakeProcedure("choking", T("Бўғилиш", "Bo'g'ilish", "Удушье"),
            T("Нафас йўллари тўсилганда биринчи ёрдам", "Nafas yo'llari to'silganda birinchi yordam", "Первая помощь при закупорке дыхательных путей"), 6, now,
            [
                Step(T("5 марта орқадан уриш", "5 marta orqadan urish", "5 ударов по спине"),
                    T("Жабрланувчининг орқасидан кафт билан кураклар орасига 5 марта уринг", "Jabrlanuvchining orqasidan kaft bilan kuraklar orasiga 5 marta uring", "Нанесите 5 ударов ладонью между лопатками пострадавшего"), 1),
                Step(T("5 марта Геймлих усули", "5 marta Geymlih usuli", "5 приёмов Геймлиха"),
                    T("Орқадан қучоқлаб, мушт билан ошқозон соҳасига 5 марта босинг", "Orqadan quchoqlab, musht bilan oshqozon sohasiga 5 marta bosing", "Обхватите сзади и сделайте 5 толчков кулаком в область живота"), 2),
                Step(T("Навбатлашиб давом эттириш", "Navbatlashib davom ettirish", "Чередование"),
                    T("Чет тана чиққунча 5 уриш ва 5 босишни навбатлаштиринг", "Chet tana chiqquncha 5 urish va 5 bosishni navbatlashtiring", "Чередуйте 5 ударов по спине и 5 толчков в живот до извлечения инородного тела"), 3),
                Step(T("Ҳушини йўқотса", "Hushini yo'qotsa", "Потеря сознания"),
                    T("Агар ҳушини йўқотса, юрак-ўпка реанимациясини бошланг", "Agar hushini yo'qotsa, yurak-o'pka reanimatsiyasini boshlang", "Если пострадавший потерял сознание, начните сердечно-лёгочную реанимацию"), 4),
            ]);

        var shock = MakeProcedure("shock", T("Шок ҳолати", "Shok holati", "Шоковое состояние"),
            T("Шок белгилари ва биринчи ёрдам", "Shok belgilari va birinchi yordam", "Признаки шока и первая помощь"), 7, now,
            [
                Step(T("Чалқанча ёткизиш", "Chalqancha yotqizish", "Уложить на спину"),
                    T("Жабрланувчини чалқанча ёткизинг", "Jabrlanuvchini chalqancha yotqizing", "Уложите пострадавшего на спину"), 1),
                Step(T("Оёқларини кўтариш", "Oyoqlarini ko'tarish", "Поднять ноги"),
                    T("Оёқларини 20-30 см кўтаринг", "Oyoqlarini 20-30 sm ko'taring", "Поднимите ноги на 20-30 см"), 2),
                Step(T("Кийимларни бўшатиш", "Kiyimlarni bo'shatish", "Ослабить одежду"),
                    T("Тор кийимларни бўшатинг", "Tor kiyimlarni bo'shating", "Ослабьте тесную одежду"), 3),
                Step(T("Иссиқ тутиш", "Issiq tutish", "Согреть"),
                    T("Кўрпа ёки кийим билан ёпинг", "Ko'rpa yoki kiyim bilan yoping", "Укройте одеялом или одеждой"), 4),
                Step(T("Тез ёрдамни кутиш", "Tez yordamni kutish", "Ожидание скорой"),
                    T("103 га қўнғироқ қилинг ва тез ёрдамни кутинг", "103 ga qo'ng'iroq qiling va tez yordamni kuting", "Позвоните 103 и дождитесь скорой помощи"), 5),
            ]);

        var poisoning = MakeProcedure("poisoning", T("Заҳарланиш", "Zaharlanish", "Отравление"),
            T("Заҳарланишда биринчи ёрдам кўрсатиш", "Zaharlanishda birinchi yordam ko'rsatish", "Первая помощь при отравлении"), 8, now,
            [
                Step(T("Заҳар манбаини аниқлаш", "Zahar manba'ini aniqlash", "Определение источника яда"),
                    T("Нимадан заҳарланганини аниқланг", "Nimadan zaharlanganini aniqlang", "Определите источник отравления"), 1),
                Step(T("Тез ёрдамни чақириш", "Tez yordamni chaqirish", "Вызов скорой"),
                    T("103 га қўнғироқ қилинг ва заҳар турини айтинг", "103 ga qo'ng'iroq qiling va zahar turini ayting", "Позвоните 103 и сообщите тип яда"), 2),
                Step(T("Қусдирмаслик", "Qusdirmaslik", "Не вызывать рвоту"),
                    T("Кислота ёки ишқор ичган бўлса асло қусдирманг", "Kislota yoki ishqor ichgan bo'lsa aslo qusdirmang", "Никогда не вызывайте рвоту при отравлении кислотой или щёлочью"), 3),
                Step(T("Нафас олишни таъминлаш", "Nafas olishni ta'minlash", "Обеспечение дыхания"),
                    T("Ёнига ёткизинг ва нафас олишини кузатинг", "Yoniga yotqizing va nafas olishini kuzating", "Уложите набок и следите за дыханием"), 4),
            ]);

        db.FirstAidProcedures.AddRange([cpr, wound, fracture, burns, bleeding, choking, shock, poisoning]);
        logger.LogInformation("Seeded {Count} first aid procedures", 8);
    }

    private async Task SeedGlossaryCategoriesAndTermsAsync(CancellationToken ct)
    {
        if (await db.GlossaryCategories.AnyAsync(ct))
            return;

        var now = DateTimeOffset.UtcNow;

        // Category 1: Road Signs
        var signsId = Guid.NewGuid();
        var signs = new GlossaryCategory { Id = signsId, Slug = "road-signs", Name = T("Йўл белгилари", "Yo'l belgilari", "Дорожные знаки"), Icon = "SignpostBig", SortOrder = 1, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(signsId, T("Огоҳлантирувчи белги", "Ogohlantirivchi belgi", "Предупреждающий знак"), T("Хавфли участка ёки шарт-шароит ҳақида огоҳлантирадиган учбурчак шаклидаги белги", "Xavfli uchastkа yoki shart-sharoit haqida ogohlantiradigan uchburchak shaklidagi belgi", "Знак треугольной формы, предупреждающий об опасном участке или условиях"), 1, now),
            GTerm(signsId, T("Тақиқловчи белги", "Taqiqlovchi belgi", "Запрещающий знак"), T("Маълум ҳаракатларни тақиқлайдиган думалоқ қизил рамкали белги", "Ma'lum harakatlarni taqiqlаydigan dumaloq qizil ramkali belgi", "Круглый знак с красной каймой, запрещающий определённые действия"), 2, now),
            GTerm(signsId, T("Буюрувчи белги", "Buyuruvchi belgi", "Предписывающий знак"), T("Маълум ҳаракатларни бажаришни буюрадиган кўк думалоқ белги", "Ma'lum harakatlarni bajarishni buyuradigan ko'k dumaloq belgi", "Круглый знак синего цвета, предписывающий определённые действия"), 3, now),
            GTerm(signsId, T("Имтиёз белгиси", "Imtiyoz belgisi", "Знак приоритета"), T("Чоррахаларда ўтиш тартибини белгилайдиган белги", "Chorrahаlarda o'tish tartibini belgilаydigan belgi", "Знак, определяющий порядок проезда перекрёстков"), 4, now),
            GTerm(signsId, T("Ахборот белгиси", "Axborot belgisi", "Информационный знак"), T("Йўл шароити ва йўналишлар ҳақида маълумот берувчи белги", "Yo'l sharoiti va yo'nalishlar haqida ma'lumot beruvchi belgi", "Знак, информирующий о дорожных условиях и направлениях"), 5, now),
            GTerm(signsId, T("Хизмат белгиси", "Xizmat belgisi", "Знак сервиса"), T("Яқин атрофдаги хизмат кўрсатиш жойлари ҳақида маълумот берувчи белги", "Yaqin atrofdagi xizmat ko'rsatish joylari haqida ma'lumot beruvchi belgi", "Знак, информирующий о расположении объектов сервиса"), 6, now),
            GTerm(signsId, T("Қўшимча белги", "Qo'shimcha belgi", "Табличка"), T("Асосий белгига қўшимча маълумот берувчи кичик тўртбурчак белги", "Asosiy belgiga qo'shimcha ma'lumot beruvchi kichik to'rtburchak belgi", "Маленькая прямоугольная табличка, дополняющая основной знак"), 7, now),
        }};

        // Category 2: Traffic Rules
        var rulesId = Guid.NewGuid();
        var rules = new GlossaryCategory { Id = rulesId, Slug = "traffic-rules", Name = T("Йўл ҳаракати қоидалари", "Yo'l harakati qoidalari", "Правила дорожного движения"), Icon = "BookOpen", SortOrder = 2, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(rulesId, T("Чоррахa", "Chorraha", "Перекрёсток"), T("Икки ёки ундан ортиқ йўлнинг бир сатҳда кесишган жойи", "Ikki yoki undan ortiq yo'lning bir sathda kesishgan joyi", "Место пересечения двух или более дорог на одном уровне"), 1, now),
            GTerm(rulesId, T("Ҳаракат қатнашчиси", "Harakat qatnashchisi", "Участник дорожного движения"), T("Йўлда ҳаракатда иштирок этаётган шахс", "Yo'lda harakatda ishtirok etayotgan shaxs", "Лицо, участвующее в дорожном движении"), 2, now),
            GTerm(rulesId, T("Пиёда", "Piyoda", "Пешеход"), T("Транспорт воситасидан ташқарида йўлда юраётган шахс", "Transport vositasidan tashqarida yo'lda yurayotgan shaxs", "Лицо, находящееся на дороге вне транспортного средства"), 3, now),
            GTerm(rulesId, T("Ҳайдовчи", "Haydovchi", "Водитель"), T("Транспорт воситасини бошқараётган шахс", "Transport vositasini boshqarayotgan shaxs", "Лицо, управляющее транспортным средством"), 4, now),
            GTerm(rulesId, T("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости"), T("Маълум йўл участкасида рухсат этилган максимал тезлик", "Ma'lum yo'l uchаstkasida ruxsat etilgan maksimal tezlik", "Максимальная разрешённая скорость на определённом участке дороги"), 5, now),
            GTerm(rulesId, T("Қувиб ўтиш", "Quvib o'tish", "Обгон"), T("Олдиндаги транспортни қарши йўлга чиқиб ўтиш", "Oldindagi transportni qarshi yo'lga chiqib o'tish", "Опережение транспортного средства с выездом на встречную полосу"), 6, now),
            GTerm(rulesId, T("Тўхтаб туриш", "To'xtab turish", "Стоянка"), T("5 дақиқадан ортиқ вақтга транспортни тўхтатиш", "5 daqiqadan ortiq vaqtga transportni to'xtatish", "Прекращение движения транспортного средства более чем на 5 минут"), 7, now),
            GTerm(rulesId, T("Тўхташ", "To'xtash", "Остановка"), T("5 дақиқагача вақтга транспортни тўхтатиш", "5 daqiqagacha vaqtga transportni to'xtatish", "Прекращение движения транспортного средства на срок до 5 минут"), 8, now),
        }};

        // Category 3: Vehicles
        var vehiclesId = Guid.NewGuid();
        var vehicles = new GlossaryCategory { Id = vehiclesId, Slug = "vehicles", Name = T("Транспорт воситалари", "Transport vositalari", "Транспортные средства"), Icon = "Car", SortOrder = 3, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(vehiclesId, T("Механик транспорт воситаси", "Mexanik transport vositasi", "Механическое транспортное средство"), T("Двигатель ёрдамида ҳаракатланадиган транспорт воситаси", "Dvigatel yordamida harakatlanadigan transport vositasi", "Транспортное средство, приводимое в движение двигателем"), 1, now),
            GTerm(vehiclesId, T("Мотоцикл", "Mototsikl", "Мотоцикл"), T("Икки ғилдиракли механик транспорт воситаси", "Ikki g'ildirakli mexanik transport vositasi", "Двухколёсное механическое транспортное средство"), 2, now),
            GTerm(vehiclesId, T("Велосипед", "Velosiped", "Велосипед"), T("Мускул кучи билан ҳаракатланадиган икки ғилдиракли транспорт", "Muskul kuchi bilan harakatlanadigan ikki g'ildirakli transport", "Двухколёсное транспортное средство, приводимое в движение мускульной силой"), 3, now),
            GTerm(vehiclesId, T("Автобус", "Avtobus", "Автобус"), T("8 дан ортиқ йўловчи ўриндиқли транспорт воситаси", "8 dan ortiq yo'lovchi o'rindiqli transport vositasi", "Транспортное средство с количеством пассажирских мест более 8"), 4, now),
            GTerm(vehiclesId, T("Юк автомобили", "Yuk avtomobili", "Грузовой автомобиль"), T("Юк ташиш учун мўлжалланган транспорт воситаси", "Yuk tashish uchun mo'ljallangan transport vositasi", "Транспортное средство, предназначенное для перевозки грузов"), 5, now),
        }};

        // Category 4: Road Types
        var roadsId = Guid.NewGuid();
        var roads = new GlossaryCategory { Id = roadsId, Slug = "road-types", Name = T("Йўл турлари", "Yo'l turlari", "Типы дорог"), Icon = "Route", SortOrder = 4, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(roadsId, T("Автомагистрал", "Avtomagistral", "Автомагистраль"), T("Қарши йўналишдаги оқимлар ажратилган юқори тезликли йўл", "Qarshi yo'nalishdagi oqimlar ajratilgan yuqori tezlikli yo'l", "Скоростная дорога с разделёнными встречными потоками"), 1, now),
            GTerm(roadsId, T("Аҳоли пункти", "Aholi punkti", "Населённый пункт"), T("Номи кўрсатилган белги билан белгиланган ҳудуд", "Nomi ko'rsatilgan belgi bilan belgilangan hudud", "Территория, обозначенная знаком с названием"), 2, now),
            GTerm(roadsId, T("Пиёдалар ўтиш жойи", "Piyodalar o'tish joyi", "Пешеходный переход"), T("Пиёдаларнинг йўлни кесиб ўтиши учун ажратилган жой", "Piyodalarning yo'lni kesib o'tishi uchun ajratilgan joy", "Место, предназначенное для перехода пешеходов через дорогу"), 3, now),
            GTerm(roadsId, T("Темир йўл кесишмаси", "Temir yo'l kesishmasi", "Железнодорожный переезд"), T("Автомобил йўли ва темир йўлнинг кесишган жойи", "Avtomobil yo'li va temir yo'lning kesishgan joyi", "Место пересечения автомобильной и железной дороги"), 4, now),
        }};

        // Category 5: Safety
        var safetyId = Guid.NewGuid();
        var safety = new GlossaryCategory { Id = safetyId, Slug = "safety", Name = T("Хавфсизлик", "Xavfsizlik", "Безопасность"), Icon = "ShieldCheck", SortOrder = 5, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(safetyId, T("Хавфсизлик камари", "Xavfsizlik kamari", "Ремень безопасности"), T("Ҳайдовчи ва йўловчиларни ҳимоя қиладиган камар", "Haydovchi va yo'lovchilarni himoya qiladigan kamar", "Ремень, защищающий водителя и пассажиров при столкновении"), 1, now),
            GTerm(safetyId, T("Ёритиш чироқлари", "Yoritish chiroqlari", "Световые приборы"), T("Транспорт воситасининг ташқи ёритиш ускуналари", "Transport vositasining tashqi yoritish uskunalari", "Внешние осветительные приборы транспортного средства"), 2, now),
            GTerm(safetyId, T("Тормоз тизими", "Tormoz tizimi", "Тормозная система"), T("Транспортни секинлаштириш ва тўхтатиш учун ишлатиладиган тизим", "Transportni sekinlashtirish va to'xtatish uchun ishlatiladigan tizim", "Система для замедления и остановки транспортного средства"), 3, now),
            GTerm(safetyId, T("Хавфли юк", "Xavfli yuk", "Опасный груз"), T("Портлаш, заҳарланиш ёки ёнғин хавфи бор юк", "Portlash, zaharlanish yoki yong'in xavfi bor yuk", "Груз, представляющий опасность взрыва, отравления или пожара"), 4, now),
            GTerm(safetyId, T("Фалокат белгиси", "Falokat belgisi", "Аварийный знак"), T("Мажбурий тўхташда кўрсатиладиган учбурчак белги", "Majburiy to'xtashda ko'rsatiladigan uchburchak belgi", "Треугольный знак, выставляемый при вынужденной остановке"), 5, now),
        }};

        // Category 6: Medical Aid
        var medicalId = Guid.NewGuid();
        var medical = new GlossaryCategory { Id = medicalId, Slug = "medical-aid", Name = T("Тиббий ёрдам", "Tibbiy yordam", "Медицинская помощь"), Icon = "Heart", SortOrder = 6, CreatedAt = now, UpdatedAt = now, Terms = new List<GlossaryTerm>
        {
            GTerm(medicalId, T("Биринчи тиббий ёрдам", "Birinchi tibbiy yordam", "Первая медицинская помощь"), T("Шикастланган шахсга дастлабки тиббий ёрдам кўрсатиш", "Shikastlangan shaxsga dastlabki tibbiy yordam ko'rsatish", "Оказание первичной медицинской помощи пострадавшему"), 1, now),
            GTerm(medicalId, T("Юрак-ўпка реанимацияси", "Yurak-o'pka reanimatsiyasi", "Сердечно-лёгочная реанимация"), T("Юрак уриши ва нафас олиш тўхтаганда бажариладиган жараён", "Yurak urishi va nafas olish to'xtaganda bajariladigan jarayon", "Процедура, выполняемая при остановке сердцебиения и дыхания"), 2, now),
            GTerm(medicalId, T("Жгут", "Jgut", "Жгут"), T("Қон кетишни тўхтатиш учун маҳкамланадиган тасма", "Qon ketishni to'xtatish uchun mahkamlаnadigan tasma", "Лента для остановки кровотечения путём пережатия сосуда"), 3, now),
            GTerm(medicalId, T("Шок", "Shok", "Шок"), T("Қон айланиш бузилиши натижасида юзага келадиган хавфли ҳолат", "Qon aylanishi buzilishi natijasida yuzaga keladigan xavfli holat", "Опасное состояние, вызванное нарушением кровообращения"), 4, now),
        }};

        db.GlossaryCategories.AddRange([signs, rules, vehicles, roads, safety, medical]);
        logger.LogInformation("Seeded 6 glossary categories with terms");
    }

    // ── Phase 2 Content Helpers ──

    private static TrafficFine Fine(string article, LocalizedText violation, long penaltyTiyins, long? maxPenaltyTiyins, int sort, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(), ArticleNumber = article,
            ViolationDescription = violation, PenaltyAmountTiyins = penaltyTiyins,
            PenaltyMaxTiyins = maxPenaltyTiyins, SortOrder = sort, IsActive = true,
            CreatedAt = now, UpdatedAt = now
        };

    private static HazardLabel Hazard(string slug, string hazardClass, LocalizedText name, LocalizedText description, int sort, DateTimeOffset now, string imageUrl = "") =>
        new()
        {
            Id = Guid.NewGuid(), Slug = slug, HazardClass = hazardClass,
            Name = name, Description = description, ImageUrl = imageUrl,
            SortOrder = sort, CreatedAt = now, UpdatedAt = now
        };

    private static (LocalizedText Title, LocalizedText Description, int Order) Step(LocalizedText title, LocalizedText description, int order) =>
        (title, description, order);

    private static FirstAidProcedure MakeProcedure(string slug, LocalizedText name, LocalizedText summary, int sort, DateTimeOffset now,
        (LocalizedText Title, LocalizedText Description, int Order)[] steps)
    {
        var procId = Guid.NewGuid();
        return new FirstAidProcedure
        {
            Id = procId, Slug = slug, Name = name, Summary = summary,
            IconUrl = string.Empty, SortOrder = sort,
            CreatedAt = now, UpdatedAt = now,
            Steps = steps.Select(s => new FirstAidStep
            {
                Id = Guid.NewGuid(), FirstAidProcedureId = procId,
                Title = s.Title, Description = s.Description,
                StepOrder = s.Order, CreatedAt = now, UpdatedAt = now
            }).ToList()
        };
    }

    private static GlossaryTerm GTerm(Guid categoryId, LocalizedText term, LocalizedText definition, int sort, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(), GlossaryCategoryId = categoryId,
            Term = term, Definition = definition,
            SortOrder = sort, RelatedQuestionIds = [],
            CreatedAt = now, UpdatedAt = now
        };

    // ── Helpers ──

    private static Category MakeCategory(string slug, string uz, string uzLatin, string ru, int sort, string? parentSlug) =>
        new()
        {
            Id = Guid.NewGuid(), Slug = slug,
            Name = new LocalizedText(uz, uzLatin, ru),
            Description = new LocalizedText(uz, uzLatin, ru),
            SortOrder = sort, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };

    private static ExamPoolRule MakePoolRule(Guid templateId, Guid categoryId, Difficulty? difficulty, int count, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(), ExamTemplateId = templateId, CategoryId = categoryId,
            Difficulty = difficulty, QuestionCount = count, CreatedAt = now, UpdatedAt = now
        };

    private static LocalizedText T(string uz, string uzLatin, string ru) => new(uz, uzLatin, ru);

    private static (LocalizedText Text, bool IsCorrect) A(string uz, string uzLatin, string ru, bool correct) =>
        (new LocalizedText(uz, uzLatin, ru), correct);

    private static Question Q(Guid categoryId, int ticket, Difficulty difficulty, DateTimeOffset now,
        LocalizedText text, LocalizedText explanation,
        params (LocalizedText Text, bool IsCorrect)[] answers)
    {
        var qId = Guid.NewGuid();
        return new Question
        {
            Id = qId, CategoryId = categoryId, TicketNumber = ticket,
            Difficulty = difficulty, LicenseCategory = LicenseCategory.Both,
            Status = QuestionStatus.Active, Text = text, Explanation = explanation,
            CreatedAt = now, UpdatedAt = now,
            AnswerOptions = answers.Select((a, i) => new AnswerOption
            {
                Id = Guid.NewGuid(), QuestionId = qId,
                Text = a.Text, IsCorrect = a.IsCorrect, SortOrder = i + 1,
                CreatedAt = now, UpdatedAt = now
            }).ToList()
        };
    }
}
