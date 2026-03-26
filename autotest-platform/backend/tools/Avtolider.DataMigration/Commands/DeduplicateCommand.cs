using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Smart deduplication: fuzzy-matches questions by normalized Russian text (Levenshtein < 20%).
/// Instead of just keeping one and deleting the other, it MERGES the best fields from both:
///   - Image: prefer the one that has it
///   - Explanation: prefer the one that has it
///   - Category: prefer themed category over flat "uncategorized"
///   - UZ Latin: prefer native over auto-transliterated (non-empty wins)
///   - Answer options: keep from the richer source
/// The merged record survives; the duplicate is soft-deleted (or hard-deleted with --hard).
/// </summary>
public static class DeduplicateCommand
{
    public static async Task ExecuteAsync(
        MigrationContext ctx,
        bool hardDelete = false,
        CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║      SMART MERGE-DEDUPLICATION       ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        if (hardDelete)
            Console.WriteLine("  [HARD DELETE] Duplicates will be permanently deleted.");
        else
            Console.WriteLine("  [SOFT DELETE] Duplicates will be marked IsActive=false.");
        Console.WriteLine();

        // Load all active questions with fields needed for merge decisions
        Console.WriteLine("  Loading all active questions from DB...");
        var questions = await ctx.Db.Questions
            .Include(q => q.AnswerOptions)
            .Where(q => q.IsActive)
            .ToListAsync(ct);

        Console.WriteLine($"  Loaded {questions.Count} active questions");

        if (questions.Count < 2)
        {
            Console.WriteLine("  Nothing to deduplicate.");
            return;
        }

        // Resolve the "uncategorized" category id to know what's themed vs flat
        var uncategorizedSlug = ctx.DefaultApkCategorySlug;
        var uncategorizedCategory = await ctx.Db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == uncategorizedSlug, ct);
        var uncategorizedId = uncategorizedCategory?.Id ?? Guid.Empty;

        // Pre-normalize all Russian texts
        var normalized = questions
            .Select(q => (q, norm: UzbekTransliterator.Normalize(q.Text.Ru)))
            .Where(x => x.norm.Length > 10)
            .ToList();

        Console.WriteLine($"  Comparing {normalized.Count} questions for duplicates...");

        // Union-Find to group duplicates
        var parent = new Dictionary<Guid, Guid>();
        foreach (var (q, _) in normalized)
            parent[q.Id] = q.Id;

        Guid Find(Guid id)
        {
            if (parent[id] != id)
                parent[id] = Find(parent[id]);
            return parent[id];
        }

        void Union(Guid a, Guid b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
                parent[rootA] = rootB;
        }

        // O(n^2) comparison
        int comparisons = 0;
        for (int i = 0; i < normalized.Count; i++)
        {
            var (qi, normI) = normalized[i];
            for (int j = i + 1; j < normalized.Count; j++)
            {
                var (qj, normJ) = normalized[j];

                var maxLen = Math.Max(normI.Length, normJ.Length);
                var minLen = Math.Min(normI.Length, normJ.Length);
                if ((double)(maxLen - minLen) / maxLen > 0.35)
                    continue;

                if (LevenshteinDistance.AreSimilar(normI, normJ, threshold: 0.20))
                    Union(qi.Id, qj.Id);

                comparisons++;
            }

            if (i % 100 == 0)
                Console.Write($"\r  Comparing: {i}/{normalized.Count}...");
        }
        Console.WriteLine($"\r  Compared {comparisons:N0} pairs.");

        // Group by root
        var groups = normalized
            .GroupBy(x => Find(x.q.Id))
            .Where(g => g.Count() > 1)
            .ToList();

        Console.WriteLine($"  Duplicate groups found: {groups.Count}");
        if (groups.Count == 0)
        {
            Console.WriteLine("  No duplicates found. Data is clean.");
            return;
        }

        // SMART MERGE: for each group, pick the best of each field
        // EF Core owned entities (LocalizedText via ToJson) cannot be reassigned on tracked entities.
        // We collect merge operations and apply them via raw SQL / ExecuteUpdateAsync per field.
        var toRemove = new List<Guid>();
        var mergeOps = new List<MergeOperation>();

        foreach (var group in groups)
        {
            var members = group.Select(x => x.q).ToList();

            // Pick base keeper: prefer one with image + explanation + themed category
            var keeper = PickBestKeeper(members, uncategorizedId);
            var duplicates = members.Where(m => m.Id != keeper.Id).ToList();

            foreach (var dup in duplicates)
            {
                // Merge image
                if (string.IsNullOrEmpty(keeper.ImageUrl) && !string.IsNullOrEmpty(dup.ImageUrl))
                    mergeOps.Add(new MergeOperation(keeper.Id, MergeField.Image, dup.ImageUrl, dup.ThumbnailUrl));

                // Merge explanation (owned entity — must use raw SQL)
                if (string.IsNullOrEmpty(keeper.Explanation.Ru) && !string.IsNullOrEmpty(dup.Explanation.Ru))
                    mergeOps.Add(new MergeOperation(keeper.Id, MergeField.Explanation,
                        ExplJson: new LocalizedText(
                            dup.Explanation.Uz,
                            dup.Explanation.UzLatin,
                            dup.Explanation.Ru)));

                // Merge category
                if (keeper.CategoryId == uncategorizedId && dup.CategoryId != uncategorizedId)
                    mergeOps.Add(new MergeOperation(keeper.Id, MergeField.Category, CategoryId: dup.CategoryId));

                // Merge UZ Latin text (owned entity — must use raw SQL)
                if (string.IsNullOrEmpty(keeper.Text.UzLatin) && !string.IsNullOrEmpty(dup.Text.UzLatin))
                    mergeOps.Add(new MergeOperation(keeper.Id, MergeField.TextUzLatin,
                        TextJson: new LocalizedText(keeper.Text.Uz, dup.Text.UzLatin, keeper.Text.Ru)));

                // Merge UZ Latin explanation
                if (!string.IsNullOrEmpty(keeper.Explanation.Ru)
                    && string.IsNullOrEmpty(keeper.Explanation.UzLatin)
                    && !string.IsNullOrEmpty(dup.Explanation.UzLatin))
                    mergeOps.Add(new MergeOperation(keeper.Id, MergeField.ExplanationUzLatin,
                        ExplJson: new LocalizedText(
                            keeper.Explanation.Uz.Length > 0 ? keeper.Explanation.Uz : dup.Explanation.Uz,
                            dup.Explanation.UzLatin,
                            keeper.Explanation.Ru)));
            }

            Console.WriteLine($"  Merged: '{TruncateText(keeper.Text.Ru, 55)}' (kept #{keeper.Id:N})");
            toRemove.AddRange(duplicates.Select(d => d.Id));
        }

        Console.WriteLine();
        Console.WriteLine($"  Total merge operations: {mergeOps.Count}");
        Console.WriteLine($"  Total duplicates to remove: {toRemove.Count}");

        if (ctx.DryRun)
        {
            Console.WriteLine("  [DRY RUN] Skipping actual writes.");
            return;
        }

        // Clear tracker — we'll use ExecuteUpdateAsync which bypasses tracking
        ctx.Db.ChangeTracker.Clear();

        // Apply merge operations via ExecuteUpdateAsync (one per keeper per field type)
        int mergedFields = 0;
        foreach (var op in mergeOps)
        {
            try
            {
                switch (op.Field)
                {
                    case MergeField.Image:
                        await ctx.Db.Questions
                            .Where(q => q.Id == op.KeeperId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(q => q.ImageUrl, op.ImageUrl)
                                .SetProperty(q => q.ThumbnailUrl, op.ThumbUrl), ct);
                        break;

                    case MergeField.Category:
                        await ctx.Db.Questions
                            .Where(q => q.Id == op.KeeperId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(q => q.CategoryId, op.CategoryId!.Value), ct);
                        break;

                    case MergeField.Explanation:
                    case MergeField.ExplanationUzLatin:
                        // Owned entity (JSONB) — use raw SQL
                        await ctx.Db.Database.ExecuteSqlAsync(
                            $"""UPDATE autotest."Questions" SET "Explanation" = CAST({SerializeJson(op.ExplJson!)} AS jsonb) WHERE "Id" = {op.KeeperId}""", ct);
                        break;

                    case MergeField.TextUzLatin:
                        // Owned entity (JSONB) — use raw SQL
                        await ctx.Db.Database.ExecuteSqlAsync(
                            $"""UPDATE autotest."Questions" SET "Text" = CAST({SerializeJson(op.TextJson!)} AS jsonb) WHERE "Id" = {op.KeeperId}""", ct);
                        break;
                }
                mergedFields++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [WARN] Merge op failed for {op.KeeperId}: {ex.Message}");
            }
        }

        // Remove duplicates in batches
        const int deleteBatchSize = 100;
        int removed = 0;
        foreach (var chunk in toRemove.Chunk(deleteBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (hardDelete)
                {
                    await ctx.Db.Questions
                        .Where(q => chunk.Contains(q.Id))
                        .ExecuteDeleteAsync(ct);
                }
                else
                {
                    await ctx.Db.Questions
                        .Where(q => chunk.Contains(q.Id))
                        .ExecuteUpdateAsync(s => s.SetProperty(q => q.IsActive, false), ct);
                }
                removed += chunk.Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERROR] Failed to remove batch: {ex.Message}");
                ctx.Stats.RecordError(chunk.Length);
            }
        }

        ctx.Stats.RecordDuplicateRemoved(removed);
        Console.WriteLine($"  Deduplication done: {removed} duplicates removed, {mergedFields} fields merged.");

        // Reassign ticket numbers sequentially after dedup
        await ReassignTicketNumbersAsync(ctx, ct);
    }

    /// <summary>
    /// After dedup, ticket numbers are inconsistent (gaps, uneven sizes).
    /// Reassign sequentially: order by Category → Difficulty → Id, every 20 = new ticket.
    /// </summary>
    private static async Task ReassignTicketNumbersAsync(MigrationContext ctx, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("  Reassigning ticket numbers...");

        var activeQuestions = await ctx.Db.Questions
            .Where(q => q.IsActive)
            .OrderBy(q => q.CategoryId)
            .ThenBy(q => q.Difficulty)
            .ThenBy(q => q.Id)
            .ToListAsync(ct);

        if (activeQuestions.Count == 0)
        {
            Console.WriteLine("  No active questions to assign tickets.");
            return;
        }

        const int questionsPerTicket = 20;
        for (int i = 0; i < activeQuestions.Count; i++)
        {
            if (!ctx.DryRun)
                activeQuestions[i].TicketNumber = (i / questionsPerTicket) + 1;
        }

        int totalTickets = (activeQuestions.Count - 1) / questionsPerTicket + 1;
        int lastTicketSize = activeQuestions.Count % questionsPerTicket;
        if (lastTicketSize == 0) lastTicketSize = questionsPerTicket;

        if (!ctx.DryRun)
        {
            await ctx.Db.SaveChangesAsync(ct);
            ctx.Db.ChangeTracker.Clear();
        }

        Console.WriteLine($"  Tickets reassigned: {totalTickets} tickets ({questionsPerTicket} Q each, last has {lastTicketSize})");
    }

    // Pick the best base keeper — weighted scoring:
    //   +4 for themed category (not uncategorized)
    //   +3 for having image
    //   +2 for having explanation
    //   +1 for having UZ Latin text
    private static Question PickBestKeeper(List<Question> members, Guid uncategorizedId)
    {
        return members.MaxBy(q =>
            (q.CategoryId != uncategorizedId ? 4 : 0) +
            (!string.IsNullOrEmpty(q.ImageUrl) ? 3 : 0) +
            (!string.IsNullOrEmpty(q.Explanation.Ru) ? 2 : 0) +
            (!string.IsNullOrEmpty(q.Text.UzLatin) ? 1 : 0)
        )!;
    }

    private static string TruncateText(string text, int maxLen) =>
        text.Length > maxLen ? text[..maxLen] + "..." : text;

    // Serialize LocalizedText to JSON matching EF Core's ToJson() column format
    private static string SerializeJson(LocalizedText lt) =>
        System.Text.Json.JsonSerializer.Serialize(new { lt.Uz, lt.UzLatin, lt.Ru });

    private enum MergeField { Image, Explanation, ExplanationUzLatin, Category, TextUzLatin }

    private record MergeOperation(
        Guid KeeperId,
        MergeField Field,
        string? ImageUrl = null,
        string? ThumbUrl = null,
        Guid? CategoryId = null,
        LocalizedText? ExplJson = null,
        LocalizedText? TextJson = null);
}
