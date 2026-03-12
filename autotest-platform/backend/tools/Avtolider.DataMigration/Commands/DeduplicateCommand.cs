using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Persistence;
using Avtolider.DataMigration.Services;
using Microsoft.EntityFrameworkCore;

namespace Avtolider.DataMigration.Commands;

/// <summary>
/// Deduplication pass: fuzzy-matches all questions by their normalized Russian text.
/// Two questions are considered duplicates if Levenshtein distance < 20% of the longer string.
/// Keeps the "richer" version (has image + explanation > has only image > has only explanation > plain).
/// Soft-deletes (IsActive=false) duplicates. Use --hard flag to hard-delete.
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
        Console.WriteLine("║         DEDUPLICATION PASS           ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        if (ctx.DryRun)
            Console.WriteLine("  [DRY RUN] No data will be written.");
        if (hardDelete)
            Console.WriteLine("  [HARD DELETE] Duplicates will be permanently deleted.");
        else
            Console.WriteLine("  [SOFT DELETE] Duplicates will be marked IsActive=false.");
        Console.WriteLine();

        // Load all active questions with minimal projection for comparison
        Console.WriteLine("  Loading all active questions from DB...");
        var questions = await ctx.Db.Questions
            .AsNoTracking()
            .Where(q => q.IsActive)
            .Select(q => new QuestionSummary(
                q.Id,
                q.Text.Ru,
                q.ImageUrl,
                q.Explanation.Ru,
                q.AnswerOptions.Count))
            .ToListAsync(ct);

        Console.WriteLine($"  Loaded {questions.Count} active questions");

        if (questions.Count < 2)
        {
            Console.WriteLine("  Nothing to deduplicate.");
            return;
        }

        // Pre-normalize all Russian texts
        var normalized = questions
            .Select(q => (
                q,
                norm: UzbekTransliterator.Normalize(q.RuText)
            ))
            .Where(x => x.norm.Length > 10) // skip very short texts (unreliable match)
            .ToList();

        Console.WriteLine($"  Comparing {normalized.Count} questions for duplicates...");
        Console.WriteLine("  (This may take a while for large datasets)");
        Console.WriteLine();

        // Union-Find to group duplicates
        var parent = new Dictionary<Guid, Guid>();
        foreach (var (q, _) in normalized)
            parent[q.Id] = q.Id;

        Guid Find(Guid id)
        {
            if (parent[id] != id)
                parent[id] = Find(parent[id]); // path compression
            return parent[id];
        }

        void Union(Guid a, Guid b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
                parent[rootA] = rootB;
        }

        // O(n²) comparison — acceptable for ~1744 questions (~1.5M comparisons)
        // For larger datasets, consider LSH or block-key grouping
        int comparisons = 0;
        for (int i = 0; i < normalized.Count; i++)
        {
            var (qi, normI) = normalized[i];
            for (int j = i + 1; j < normalized.Count; j++)
            {
                var (qj, normJ) = normalized[j];

                // Fast path: skip if length difference > 30% (cannot be <20% edit distance)
                var maxLen = Math.Max(normI.Length, normJ.Length);
                var minLen = Math.Min(normI.Length, normJ.Length);
                if ((double)(maxLen - minLen) / maxLen > 0.35)
                    continue;

                if (LevenshteinDistance.AreSimilar(normI, normJ, threshold: 0.20))
                    Union(qi.Id, qj.Id);

                comparisons++;
            }

            // Progress every 100 questions
            if (i % 100 == 0)
                Console.Write($"\r  Comparing: {i}/{normalized.Count}...");
        }
        Console.WriteLine($"\r  Compared {comparisons:N0} pairs.");

        // Group by root → find duplicate groups (size > 1)
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

        // Determine richness score for each question
        // Higher = richer. Keep highest-scoring one.
        int RichnessScore(QuestionSummary q) =>
            (string.IsNullOrEmpty(q.ImageUrl) ? 0 : 2) +
            (string.IsNullOrEmpty(q.ExplanationRu) ? 0 : 1);

        var toRemove = new List<Guid>();
        foreach (var group in groups)
        {
            var members = group.Select(x => x.q).ToList();
            var keeper = members.MaxBy(RichnessScore)!;
            var duplicates = members.Where(m => m.Id != keeper.Id).ToList();

            Console.WriteLine($"  Group: keeping '{TruncateText(keeper.RuText, 60)}'");
            foreach (var dup in duplicates)
                Console.WriteLine($"    → removing: '{TruncateText(dup.RuText, 60)}'");

            toRemove.AddRange(duplicates.Select(d => d.Id));
        }

        Console.WriteLine();
        Console.WriteLine($"  Total duplicates to remove: {toRemove.Count}");

        if (ctx.DryRun)
        {
            Console.WriteLine("  [DRY RUN] Skipping actual deletion.");
            return;
        }

        // Remove in batches
        const int deleteBatchSize = 100;
        int removed = 0;
        foreach (var chunk in toRemove.Chunk(deleteBatchSize))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (hardDelete)
                {
                    // Cascade deletes answer options automatically
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
        Console.WriteLine($"  Deduplication done: {removed} duplicates removed.");
    }

    private static string TruncateText(string text, int maxLen) =>
        text.Length > maxLen ? text[..maxLen] + "..." : text;
}

// Private projection record — avoids loading full entity graph
file record QuestionSummary(
    Guid Id,
    string RuText,
    string? ImageUrl,
    string? ExplanationRu,
    int AnswerCount);
