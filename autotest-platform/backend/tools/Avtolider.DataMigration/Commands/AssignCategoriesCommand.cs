using AutoTest.Domain.Common.Enums;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Reassigns questions from the flat "APK Savollari" category into proper PDD theme categories
/// using keyword-based text matching against the question's Russian text.
/// </summary>
public static class AssignCategoriesCommand
{
    // DbSeeder slug → keywords that indicate this category
    // Keys must match slugs from DbSeeder.SeedCategoriesAsync exactly
    private static readonly Dictionary<string, string[]> ThemeKeywords = new()
    {
        // PDD Chapter 1: Термины
        ["terms"] = ["термин", "понятие", "называется", "определение", "означает слово"],
        // PDD Chapter 2: Обязанности участников
        ["participant-duties"] = ["обязан водитель", "обязанности", "участник движения", "документ при себе", "водительское удостоверение"],
        // PDD Chapter 3: Сигналы светофора и регулировщика
        ["traffic-lights"] = ["светофор", "регулировщик", "сигнал", "зелёный", "красный", "жёлтый", "мигающий"],
        // PDD Chapter 4: Предупредительные и аварийные сигналы
        ["warning-signals"] = ["аварийная сигнализация", "аварийн", "предупредительн", "звуковой сигнал", "включить фары"],
        // PDD Chapter 5: Опознавательные знаки ТС
        ["vehicle-id-signs"] = ["опознавательн", "начинающий водитель", "учебное", "инвалид", "перевозка детей", "автопоезд", "тихоходн"],
        // PDD Chapter 6: Предупреждающие знаки
        ["warning-signs"] = ["предупреждающ", "знак предупрежда"],
        // PDD Chapter 7: Знаки приоритета
        ["priority-signs"] = ["приоритет", "главная дорога", "уступи", "преимущество"],
        // PDD Chapter 8: Запрещающие знаки
        ["prohibitory-signs"] = ["запрещающ", "запрещает", "знак запрещ", "зона действия"],
        // PDD Chapter 9: Предписывающие знаки
        ["mandatory-signs"] = ["предписывающ", "предписыва"],
        // PDD Chapter 10: Информационно-указательные, сервисные, доп. знаки
        ["informational-signs"] = ["информационн", "указательн", "сервисн", "дополнительн", "табличк"],
        // PDD Chapter 11: Дорожные разметки
        ["road-markings"] = ["разметк", "сплошная линия", "прерывистая", "пешеходный переход разметк"],
        // PDD Chapter 12: Начало движения и изменение направления
        ["starting-direction"] = ["начало движения", "перестроен", "поворот", "разворот", "задний ход", "маневр"],
        // PDD Chapter 13: Расположение ТС на проезжей части
        ["vehicle-positioning"] = ["расположен", "полоса движения", "проезжая часть", "ряд", "крайн"],
        // PDD Chapter 14: Скорость движения
        ["speed-limits"] = ["скорость", "км/ч", "дистанц", "интервал", "торможен"],
        // PDD Chapter 15: Остановка и стоянка
        ["parking"] = ["остановк", "стоянк", "парков", "запрещена остановка", "запрещена стоянка"],
        // PDD Chapter 16: Обгон
        ["overtaking"] = ["обгон", "опережен", "встречн"],
        // PDD Chapter 17: Равнозначные перекрёстки
        ["equal-intersections"] = ["равнозначн", "помеха справа", "нерегулируемый перекрёсток"],
        // PDD Chapter 18: Нерегулируемые перекрёстки (со знаками приоритета)
        ["unregulated-intersections"] = ["нерегулируем", "знак приоритета на перекрёстке"],
        // PDD Chapter 19: Регулируемые перекрёстки (со светофором)
        ["regulated-intersections"] = ["регулируем", "перекрёст"],
        // PDD Chapter 20: Движение через ж/д пути
        ["railway-crossings"] = ["железнодорож", "ж/д", "переезд", "шлагбаум"],
        // PDD Chapter 21: Движение по автомагистралям
        ["highway-driving"] = ["автомагистрал", "магистрал"],
        // PDD Chapter 22: Внешние световые приборы
        ["external-lights"] = ["фар", "ближний свет", "дальний свет", "противотуман", "световые приборы", "габаритн"],
        // PDD Chapter 23: Буксировка
        ["towing"] = ["буксиров", "буксир"],
        // PDD Chapter 24: Перевозка людей
        ["passenger-transport"] = ["перевозка людей", "перевозка пассажир", "пассажир"],
        // PDD Chapter 25: Перевозка грузов
        ["cargo-transport"] = ["перевозка груз", "груз", "негабаритн"],
        // PDD Chapter 26: Условия запрещения эксплуатации ТС
        ["technical-requirements"] = ["эксплуатац", "неисправн", "тормоз", "рулев", "техосмотр", "шина"],
        // PDD Chapter 27: Безопасность управления
        ["driving-safety"] = ["безопасност", "занос", "аквапланирован", "утомлен", "видимост", "гололёд", "скользк", "мокр"],
        // PDD Chapter 28: Первая медицинская помощь
        ["first-aid"] = ["первая помощь", "медицинск", "кровотечен", "перелом", "ожог", "шок", "ранен", "пострадавш", "реанимац", "повязк"],
    };

    public static async Task ExecuteAsync(MigrationContext ctx, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║    ASSIGN CATEGORIES (keyword-based)  ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        Console.WriteLine();

        // Load all categories
        var categories = await ctx.Db.Categories
            .AsNoTracking()
            .ToListAsync(ct);

        Console.WriteLine($"  Categories in DB: {categories.Count}");

        // Find APK category (the one to reassign FROM)
        var apkCategory = categories.FirstOrDefault(c => c.Slug == ctx.DefaultApkCategorySlug);
        if (apkCategory is null)
        {
            Console.WriteLine($"  [INFO] No '{ctx.DefaultApkCategorySlug}' category found. Nothing to reassign.");
            return;
        }

        // Build keyword → category mapping (keys in ThemeKeywords are exact DbSeeder slugs)
        var categoriesBySlug = categories.ToDictionary(c => c.Slug);
        var keywordToCategory = new List<(string Keyword, Guid CategoryId, string CategoryName)>();
        foreach (var (slug, keywords) in ThemeKeywords)
        {
            if (!categoriesBySlug.TryGetValue(slug, out var category))
            {
                Console.WriteLine($"  [WARN] Category slug '{slug}' not found in DB, skipping keyword group");
                continue;
            }

            foreach (var keyword in keywords)
                keywordToCategory.Add((keyword.ToLowerInvariant(), category.Id, category.Name.Ru));
        }

        Console.WriteLine($"  Keyword rules loaded: {keywordToCategory.Count}");

        // Load questions in APK category. Includes Inactive (newly imported, awaiting
        // categorization) AND Active (older runs from before the Inactive change).
        var questions = await ctx.Db.Questions
            .Where(q => q.CategoryId == apkCategory.Id
                     && (q.Status == QuestionStatus.Active || q.Status == QuestionStatus.Inactive))
            .ToListAsync(ct);

        Console.WriteLine($"  Questions in '{ctx.DefaultApkCategorySlug}': {questions.Count}");
        if (questions.Count == 0)
        {
            Console.WriteLine("  Nothing to reassign.");
            return;
        }

        int reassigned = 0, unmatched = 0;
        foreach (var question in questions)
        {
            ct.ThrowIfCancellationRequested();

            var ruText = question.Text.Ru.ToLowerInvariant();

            // Score each category by keyword matches
            var scores = new Dictionary<Guid, int>();
            foreach (var (keyword, categoryId, _) in keywordToCategory)
            {
                if (ruText.Contains(keyword))
                {
                    scores.TryGetValue(categoryId, out var current);
                    scores[categoryId] = current + 1;
                }
            }

            if (scores.Count > 0)
            {
                var bestCategoryId = scores.OrderByDescending(kv => kv.Value).First().Key;
                if (!ctx.DryRun)
                {
                    question.CategoryId = bestCategoryId;
                    // Promote Inactive → Active once the question has a real category.
                    // Anything left in 'uncategorized' stays Inactive and is invisible
                    // to users until an admin reviews it.
                    if (question.Status == QuestionStatus.Inactive)
                        question.Status = QuestionStatus.Active;
                }
                reassigned++;
            }
            else
                unmatched++;
        }

        if (!ctx.DryRun && reassigned > 0)
        {
            await ctx.Db.SaveChangesAsync(ct);
            ctx.Db.ChangeTracker.Clear();
        }

        Console.WriteLine();
        Console.WriteLine($"  Assign done: {reassigned} reassigned (Inactive→Active), {unmatched} unmatched (stay Inactive in 'uncategorized')");
    }
}
