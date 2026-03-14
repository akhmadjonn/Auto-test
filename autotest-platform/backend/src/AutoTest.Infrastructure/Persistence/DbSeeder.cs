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

        var categories = new List<Category>
        {
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
            MakeCategory("warning-signs", "Огоҳлантирувчи белгилар", "Ogohlantirivchi belgilar", "Предупреждающие знаки", 1, "road-signs"),
            MakeCategory("priority-signs", "Устунлик белгилари", "Ustunlik belgilari", "Знаки приоритета", 2, "road-signs"),
            MakeCategory("prohibitory-signs", "Тақиқловчи белгилар", "Taqiqlovchi belgilar", "Запрещающие знаки", 3, "road-signs"),
            MakeCategory("mandatory-signs", "Мажбурий белгилар", "Majburiy belgilar", "Предписывающие знаки", 4, "road-signs"),
            MakeCategory("informational-signs", "Ахборот белгилари", "Axborot belgilari", "Информационные знаки", 5, "road-signs"),
            MakeCategory("service-signs", "Хизмат белгилари", "Xizmat belgilari", "Знаки сервиса", 6, "road-signs"),
            MakeCategory("additional-panels", "Қўшимча плиталар", "Qo'shimcha plitalar", "Дополнительные таблички", 7, "road-signs"),
            MakeCategory("cd-specific", "CD тоифаси", "CD toifasi", "Категория CD", 8, null),
        };

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
            PassingScore = 80,
            TimeLimitMinutes = 20,
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
        var apkCategory = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "apk-savollari", ct);
        var now = DateTimeOffset.UtcNow;

        if (apkCategory is null)
        {
            logger.LogWarning("Category 'apk-savollari' not found, skipping pool rules");
            return;
        }

        // Single rule: pull all 20 exam questions from APK category (542 questions available)
        db.ExamPoolRules.Add(MakePoolRule(template.Id, apkCategory.Id, null, 20, now));
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
        q.Add(Q(cats["traffic-rules"].Id, 1, Difficulty.Easy, now,
            T("Ўнг томонлама ҳаракат қоидаси нимани англатади?", "O'ng tomonlama harakat qoidasi nimani anglatadi?", "Что означает правило правостороннего движения?"),
            T("Транспорт воситалари йўлнинг ўнг томонида ҳаракатланиши керак.", "Transport vositalari yo'lning o'ng tomonida harakatlanishi kerak.", "Транспортные средства должны двигаться по правой стороне дороги."),
            A("Йўлнинг ўнг томонида юриш", "Yo'lning o'ng tomonida yurish", "Движение по правой стороне", true),
            A("Йўлнинг чап томонида юриш", "Yo'lning chap tomonida yurish", "Движение по левой стороне", false),
            A("Йўлнинг ўртасида юриш", "Yo'lning o'rtasida yurish", "Движение по центру", false),
            A("Ихтиёрий томонда юриш", "Ixtiyoriy tomonda yurish", "Движение по любой стороне", false)));

        q.Add(Q(cats["traffic-rules"].Id, 1, Difficulty.Medium, now,
            T("Ҳайдовчи йўналиш кўрсаткичини қачон ёқиши керак?", "Haydovchi yo'nalish ko'rsatkichini qachon yoqishi kerak?", "Когда водитель должен включить указатель поворота?"),
            T("Манёвр бошланишидан олдин, бошқа транспорт воситаларини огоҳлантириш учун.", "Manevr boshlanishidan oldin, boshqa transport vositalarini ogohlantirish uchun.", "Перед началом манёвра, чтобы предупредить другие транспортные средства."),
            A("Манёврдан олдин", "Manevrdan oldin", "Перед манёвром", true),
            A("Манёвр вақтида", "Manevr vaqtida", "Во время манёвра", false),
            A("Манёвр тугагач", "Manevr tugagach", "После манёвра", false),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false)));

        q.Add(Q(cats["traffic-rules"].Id, 2, Difficulty.Easy, now,
            T("Хавфсизлик камари тақиш мажбуриями?", "Xavfsizlik kamari taqish majburiymi?", "Обязательно ли пристёгиваться ремнём безопасности?"),
            T("Ҳа, ҳайдовчи ва барча йўловчилар хавфсизлик камарини тақиши шарт.", "Ha, haydovchi va barcha yo'lovchilar xavfsizlik kamarini taqishi shart.", "Да, водитель и все пассажиры обязаны пристёгиваться."),
            A("Ҳа, барчага мажбурий", "Ha, barchaga majburiy", "Да, обязательно для всех", true),
            A("Фақат ҳайдовчига", "Faqat haydovchiga", "Только для водителя", false),
            A("Фақат шаҳарда", "Faqat shaharda", "Только в городе", false),
            A("Мажбурий эмас", "Majburiy emas", "Не обязательно", false)));

        q.Add(Q(cats["traffic-rules"].Id, 2, Difficulty.Medium, now,
            T("Қайси ҳолатда чап томонга буриш тақиқланади?", "Qaysi holatda chap tomonga burish taqiqlanadi?", "В каком случае запрещён поворот налево?"),
            T("Тақиқловчи белги ёки йўл чизиғи чап буришни тақиқлаган ҳолатда.", "Taqiqlovchi belgi yoki yo'l chizig'i chap burishni taqiqlagan holatda.", "При наличии запрещающего знака или разметки, запрещающей поворот налево."),
            A("Тақиқловчи белги мавжуд бўлганда", "Taqiqlovchi belgi mavjud bo'lganda", "При наличии запрещающего знака", true),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false),
            A("Ёмғир пайтида", "Yomg'ir paytida", "Во время дождя", false),
            A("Ҳеч қачон тақиқланмайди", "Hech qachon taqiqlanmaydi", "Никогда не запрещается", false)));

        q.Add(Q(cats["traffic-rules"].Id, 3, Difficulty.Hard, now,
            T("Тезликни камайтирмасдан бурилиш нимага олиб келади?", "Tezlikni kamaytirmasdan burilish nimaga olib keladi?", "К чему приводит поворот без снижения скорости?"),
            T("Автомобил бошқарувдан чиқиши ва ағдарилиши мумкин.", "Avtomobil boshqaruvdan chiqishi va ag'darilishi mumkin.", "Автомобиль может потерять управление и опрокинуться."),
            A("Бошқарувдан чиқиш", "Boshqaruvdan chiqish", "Потеря управления", true),
            A("Ёнилғи тежаш", "Yonilg'i tejash", "Экономия топлива", false),
            A("Тезроқ бурилиш", "Tezroq burilish", "Более быстрый поворот", false),
            A("Ҳеч нарса бўлмайди", "Hech narsa bo'lmaydi", "Ничего не произойдёт", false)));

        q.Add(Q(cats["traffic-rules"].Id, 3, Difficulty.Medium, now,
            T("Кечаси аҳоли пункти ичида қайси чироқ ёқилади?", "Kechasi aholi punkti ichida qaysi chiroq yoqiladi?", "Какой свет включается ночью в населённом пункте?"),
            T("Яқинни ёритувчи фаралар ёқилади.", "Yaqinni yorituvchi faralar yoqiladi.", "Включается ближний свет фар."),
            A("Яқинни ёритувчи фара", "Yaqinni yorituvchi fara", "Ближний свет", true),
            A("Узоқни ёритувчи фара", "Uzoqni yorituvchi fara", "Дальний свет", false),
            A("Туман фаралари", "Tuman faralari", "Противотуманные фары", false),
            A("Чироқлар ёқилмайди", "Chiroqlar yoqilmaydi", "Свет не включается", false)));

        q.Add(Q(cats["traffic-rules"].Id, 1, Difficulty.Hard, now,
            T("Икки йўлли йўлда қувиб ўтиш қачон тақиқланади?", "Ikki yo'lli yo'lda quvib o'tish qachon taqiqlanadi?", "Когда запрещён обгон на двухполосной дороге?"),
            T("Кўринмаслик, чорраҳалар олдида, тепаликларда ва тақиқловчи белги бўлганда.", "Ko'rinmaslik, chorrahalar oldida, tepaliklarda va taqiqlovchi belgi bo'lganda.", "При плохой видимости, перед перекрёстками, на подъёмах и при наличии запрещающего знака."),
            A("Кўринмаслик ва чорраҳалар олдида", "Ko'rinmaslik va chorrahalar oldida", "При плохой видимости и перед перекрёстками", true),
            A("Фақат кечаси", "Faqat kechasi", "Только ночью", false),
            A("Фақат ёмғирда", "Faqat yomg'irda", "Только в дождь", false),
            A("Қувиб ўтиш доимо рухсат", "Quvib o'tish doimo ruxsat", "Обгон всегда разрешён", false)));

        q.Add(Q(cats["traffic-rules"].Id, 3, Difficulty.Easy, now,
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
        q.Add(Q(cats["priority-rules"].Id, 1, Difficulty.Easy, now,
            T("Асосий йўлда ҳаракатланаётган транспортнинг устунлиги борми?", "Asosiy yo'lda harakatlanayotgan transportning ustunligi bormi?", "Имеет ли приоритет транспорт на главной дороге?"),
            T("Ҳа, асосий йўлдаги транспорт доимо устунликка эга.", "Ha, asosiy yo'ldagi transport doimo ustunlikka ega.", "Да, транспорт на главной дороге всегда имеет приоритет."),
            A("Ҳа, доимо", "Ha, doimo", "Да, всегда", true), A("Йўқ", "Yo'q", "Нет", false), A("Фақат кундузи", "Faqat kunduzi", "Только днём", false), A("Фақат шаҳарда", "Faqat shaharda", "Только в городе", false)));

        q.Add(Q(cats["priority-rules"].Id, 2, Difficulty.Medium, now,
            T("Тенг аҳамиятли йўлларда ким биринчи ўтади?", "Teng ahamiyatli yo'llarda kim birinchi o'tadi?", "Кто проезжает первым на равнозначных дорогах?"),
            T("Ўнг томондан келаётган транспорт устунликка эга.", "O'ng tomondan kelayotgan transport ustunlikka ega.", "Приоритет имеет транспорт, приближающийся справа."),
            A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Приближающийся справа", true), A("Чапдан келаётган", "Chapdan kelayotgan", "Приближающийся слева", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Двигающийся быстрее", false), A("Каттароқ транспорт", "Kattaroq transport", "Более крупный транспорт", false)));

        q.Add(Q(cats["priority-rules"].Id, 2, Difficulty.Hard, now,
            T("Тартибга солувчи ва светофор сигнали қарама-қарши бўлса, кимга бўйсуниш керак?", "Tartibga soluvchi va svetofor signali qarama-qarshi bo'lsa, kimga bo'ysunish kerak?", "Если сигналы регулировщика и светофора противоречат, кому подчиняться?"),
            T("Тартибга солувчининг сигналларига бўйсуниш керак.", "Tartibga soluvchining signallariga bo'ysunish kerak.", "Необходимо подчиняться сигналам регулировщика."),
            A("Тартибга солувчига", "Tartibga soluvchiga", "Регулировщику", true), A("Светофорга", "Svetoforga", "Светофору", false), A("Белгиларга", "Belgilarga", "Знакам", false), A("Ўзингиз қарор қилинг", "O'zingiz qaror qiling", "Решайте сами", false)));

        q.Add(Q(cats["priority-rules"].Id, 3, Difficulty.Easy, now,
            T("Тез ёрдам автомобили сиренаси билан келаётганда нима қилиш керак?", "Tez yordam avtomobili sirenasi bilan kelayotganda nima qilish kerak?", "Что делать, когда приближается скорая помощь с сиреной?"),
            T("Йўл бериш ва четга чиқиш керак.", "Yo'l berish va chetga chiqish kerak.", "Необходимо уступить дорогу и съехать в сторону."),
            A("Йўл бериш", "Yo'l berish", "Уступить дорогу", true), A("Тезлатиш", "Tezlatish", "Ускориться", false), A("Тўхтаб туриш", "To'xtab turish", "Стоять на месте", false), A("Сигнал бериш", "Signal berish", "Подать сигнал", false)));

        q.Add(Q(cats["priority-rules"].Id, 3, Difficulty.Medium, now,
            T("Доира ҳаракатли чорраҳада ким устунликка эга?", "Doira harakatli chorrahada kim ustunlikka ega?", "Кто имеет приоритет на круговом перекрёстке?"),
            T("Доира ичида ҳаракатланаётган транспорт устунликка эга.", "Doira ichida harakatlanayotgan transport ustunlikka ega.", "Приоритет имеет транспорт, движущийся по кругу."),
            A("Доира ичидаги транспорт", "Doira ichidagi transport", "Транспорт на кругу", true), A("Кираётган транспорт", "Kirayotgan transport", "Въезжающий транспорт", false), A("Катта транспорт", "Katta transport", "Крупный транспорт", false), A("Чапдан келаётган", "Chapdan kelayotgan", "Приближающийся слева", false)));

        q.Add(Q(cats["priority-rules"].Id, 1, Difficulty.Medium, now,
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
        q.Add(Q(cats["intersections"].Id, 1, Difficulty.Medium, now,
            T("Тартибга солинмаган чорраҳада ким биринчи ўтади?", "Tartibga solinmagan chorrahada kim birinchi o'tadi?", "Кто проезжает первым на нерегулируемом перекрёстке?"),
            T("Ўнг томондан келаётган транспорт устунликка эга.", "O'ng tomondan kelayotgan transport ustunlikka ega.", "Приоритет у транспорта, приближающегося справа."),
            A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Справа", true), A("Чапдан келаётган", "Chapdan kelayotgan", "Слева", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Быстрейший", false), A("Каттароқ", "Kattaroq", "Крупнейший", false)));

        q.Add(Q(cats["intersections"].Id, 2, Difficulty.Hard, now,
            T("Чорраҳада чап буришда кимга йўл бериш керак?", "Chorrahada chap burishda kimga yo'l berish kerak?", "Кому нужно уступить при повороте налево на перекрёстке?"),
            T("Қарши томондан тўғри ва ўнгга ҳаракатланаётган транспортга.", "Qarshi tomondan to'g'ri va o'ngga harakatlanayotgan transportga.", "Встречному транспорту, движущемуся прямо и направо."),
            A("Қарши тўғри ва ўнгга кетаётганга", "Qarshi to'g'ri va o'ngga ketayotganga", "Встречному прямо и направо", true), A("Ҳеч кимга", "Hech kimga", "Никому", false), A("Чапдан келаётганга", "Chapdan kelayotganga", "Приближающемуся слева", false), A("Пиёдаларга", "Piyodalarga", "Пешеходам", false)));

        q.Add(Q(cats["intersections"].Id, 1, Difficulty.Easy, now,
            T("Чорраҳада тўхташ чизиғи нима учун?", "Chorrahada to'xtash chizig'i nima uchun?", "Для чего стоп-линия на перекрёстке?"),
            T("Тўхташ жойини белгилайди.", "To'xtash joyini belgilaydi.", "Указывает место остановки."),
            A("Тўхташ жойини белгилайди", "To'xtash joyini belgilaydi", "Обозначает место остановки", true), A("Тезлик чегараси", "Tezlik chegarasi", "Ограничение скорости", false), A("Пиёда ўтиш жойи", "Piyoda o'tish joyi", "Пешеходный переход", false), A("Парковка жойи", "Parkovka joyi", "Место парковки", false)));

        q.Add(Q(cats["intersections"].Id, 3, Difficulty.Medium, now,
            T("Т-шаклидаги чорраҳада ким устун?", "T-shaklidagi chorrahada kim ustun?", "Кто имеет приоритет на Т-образном перекрёстке?"),
            T("Асосий йўлдаги транспорт устунликка эга.", "Asosiy yo'ldagi transport ustunlikka ega.", "Приоритет у транспорта на главной дороге."),
            A("Асосий йўлдаги транспорт", "Asosiy yo'ldagi transport", "Транспорт на главной", true), A("Ўнгдан келаётган", "O'ngdan kelayotgan", "Справа", false), A("Тезроқ юраётган", "Tezroq yurayotgan", "Быстрейший", false), A("Чапдан келаётган", "Chapdan kelayotgan", "Слева", false)));

        q.Add(Q(cats["intersections"].Id, 2, Difficulty.Easy, now,
            T("Чорраҳада трамвай устунликка эгами?", "Chorrahada tramvay ustunlikka egami?", "Имеет ли трамвай приоритет на перекрёстке?"),
            T("Тенг шароитда трамвай устунликка эга.", "Teng sharoitda tramvay ustunlikka ega.", "При равных условиях трамвай имеет приоритет."),
            A("Ҳа, тенг шароитда", "Ha, teng sharoitda", "Да, при равных условиях", true), A("Йўқ", "Yo'q", "Нет", false), A("Фақат светофорда", "Faqat svetoforda", "Только на светофоре", false), A("Фақат кечаси", "Faqat kechasi", "Только ночью", false)));

        q.Add(Q(cats["intersections"].Id, 3, Difficulty.Hard, now,
            T("Чорраҳага кирган, лекин тиқилинч туфайли ўта олмаётган ҳайдовчи нима қилиши керак?", "Chorrahaga kirgan, lekin tiqilinch tufayli o'ta olmayotgan haydovchi nima qilishi kerak?", "Что делать водителю, выехавшему на перекрёсток, но не имеющему возможности проехать из-за затора?"),
            T("Чорраҳага кирмаслик керак эди, тиқилинч бўлса.", "Chorrahaga kirmaslik kerak edi, tiqilinch bo'lsa.", "Не следовало въезжать на перекрёсток при заторе."),
            A("Чорраҳага кирмаслик керак эди", "Chorrahaga kirmaslik kerak edi", "Не следовало въезжать", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Кутиш", "Kutish", "Ждать", false), A("Орқага юриш", "Orqaga yurish", "Сдать назад", false)));

        // ── pedestrians: 6 questions ──
        q.Add(Q(cats["pedestrians"].Id, 1, Difficulty.Easy, now,
            T("Пиёда ўтиш жойида ҳайдовчи нима қилиши керак?", "Piyoda o'tish joyida haydovchi nima qilishi kerak?", "Что должен делать водитель на пешеходном переходе?"),
            T("Тезликни камайтириш ва пиёдаларга йўл бериш.", "Tezlikni kamaytirish va piyodalarga yo'l berish.", "Снизить скорость и уступить дорогу пешеходам."),
            A("Тезликни камайтириш ва йўл бериш", "Tezlikni kamaytirish va yo'l berish", "Снизить скорость и уступить", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Тезлатиш", "Tezlatish", "Ускориться", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["pedestrians"].Id, 2, Difficulty.Medium, now,
            T("Кўзи ожиз пиёдага қандай муносабатда бўлиш керак?", "Ko'zi ojiz piyodaga qanday munosabatda bo'lish kerak?", "Как относиться к слепому пешеходу?"),
            T("Оқ таёқчали пиёда учраганда доимо йўл бериш мажбурий.", "Oq tayoqchali piyoda uchraganda doimo yo'l berish majburiy.", "При встрече пешехода с белой тростью обязательно уступить дорогу."),
            A("Доимо йўл бериш", "Doimo yo'l berish", "Всегда уступить", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Четлаб ўтиш", "Chetlab o'tish", "Объехать", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["pedestrians"].Id, 3, Difficulty.Easy, now,
            T("Болалар ўтиш жойи олдида нима қилиш керак?", "Bolalar o'tish joyi oldida nima qilish kerak?", "Что делать перед детским переходом?"),
            T("Тезликни камайтириш ва болаларга йўл бериш.", "Tezlikni kamaytirish va bolalarga yo'l berish.", "Снизить скорость и уступить дорогу детям."),
            A("Тезликни камайтириш", "Tezlikni kamaytirish", "Снизить скорость", true), A("Сигнал бериш", "Signal berish", "Подать сигнал", false), A("Тез ўтиб кетиш", "Tez o'tib ketish", "Быстро проехать", false), A("Тўхтамаслик", "To'xtamaslik", "Не останавливаться", false)));

        q.Add(Q(cats["pedestrians"].Id, 1, Difficulty.Medium, now,
            T("Пиёда ўтиш жойида қувиб ўтиш мумкинми?", "Piyoda o'tish joyida quvib o'tish mumkinmi?", "Разрешён ли обгон на пешеходном переходе?"),
            T("Йўқ, пиёда ўтиш жойида қувиб ўтиш тақиқланади.", "Yo'q, piyoda o'tish joyida quvib o'tish taqiqlanadi.", "Нет, обгон на пешеходном переходе запрещён."),
            A("Тақиқланади", "Taqiqlanadi", "Запрещён", true), A("Рухсат", "Ruxsat", "Разрешён", false), A("Фақат кечаси", "Faqat kechasi", "Только ночью", false), A("Секин бўлса рухсат", "Sekin bo'lsa ruxsat", "Разрешён при низкой скорости", false)));

        q.Add(Q(cats["pedestrians"].Id, 2, Difficulty.Hard, now,
            T("Пиёдалар йўлнинг қайси томонида юриши керак?", "Piyodalar yo'lning qaysi tomonida yurishi kerak?", "По какой стороне дороги должны идти пешеходы?"),
            T("Транспортга қарши — чап томонда юриш тавсия этилади.", "Transportga qarshi — chap tomonda yurish tavsiya etiladi.", "Навстречу транспорту — рекомендуется идти по левой стороне."),
            A("Транспортга қарши", "Transportga qarshi", "Навстречу транспорту", true), A("Транспорт билан бир томонда", "Transport bilan bir tomonda", "По ходу транспорта", false), A("Йўл ўртасида", "Yo'l o'rtasida", "По центру дороги", false), A("Ихтиёрий", "Ixtiyoriy", "Произвольно", false)));

        q.Add(Q(cats["pedestrians"].Id, 3, Difficulty.Medium, now,
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
            PhoneNumber = "+998901234567",
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
            IsActive = true, Text = text, Explanation = explanation,
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
