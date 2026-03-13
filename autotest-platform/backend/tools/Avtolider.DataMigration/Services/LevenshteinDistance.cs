namespace Avtolider.DataMigration.Services;

public static class LevenshteinDistance
{
    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// Uses the optimized two-row DP approach: O(n) space, O(m*n) time.
    /// </summary>
    public static int Compute(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (a == b) return 0;

        // Ensure 'a' is the shorter string to minimize allocations
        if (a.Length > b.Length)
            (a, b) = (b, a);

        var prev = new int[a.Length + 1];
        var curr = new int[a.Length + 1];

        for (int i = 0; i <= a.Length; i++)
            prev[i] = i;

        for (int j = 1; j <= b.Length; j++)
        {
            curr[0] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(
                    Math.Min(curr[i - 1] + 1, prev[i] + 1),
                    prev[i - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[a.Length];
    }

    /// <summary>
    /// Returns similarity ratio in [0..1], where 1.0 = identical, 0.0 = completely different.
    /// </summary>
    public static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        int maxLen = Math.Max(a.Length, b.Length);
        int distance = Compute(a, b);
        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Returns true if the edit distance is less than threshold% of the longer string's length.
    /// threshold=0.20 means "allow up to 20% edits" (same as task requirement).
    /// </summary>
    public static bool AreSimilar(string a, string b, double threshold = 0.20)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        int maxLen = Math.Max(a.Length, b.Length);
        int distance = Compute(a, b);
        return distance < maxLen * threshold;
    }
}
