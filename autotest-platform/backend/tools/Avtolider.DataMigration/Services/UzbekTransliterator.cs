namespace Avtolider.DataMigration.Services;

/// <summary>
/// Converts Uzbek Cyrillic text to Latin script per the modern Uzbek alphabet standard.
/// Uses straight apostrophe (') for ъ, ғ→g', ў→o' consistent with the APK data files.
/// </summary>
public static class UzbekTransliterator
{
    // Ordered: multi-char sequences first, then single-char.
    // Uppercase handled separately by detecting capital context.
    private static readonly (string Cyr, string Lat)[] Mappings =
    [
        // Two-letter Cyrillic combinations that map to two-letter Latin
        ("Ё", "Yo"), ("ё", "yo"),
        ("Ж", "J"),  ("ж", "j"),
        ("Х", "X"),  ("х", "x"),
        ("Ц", "Ts"), ("ц", "ts"),
        ("Ч", "Ch"), ("ч", "ch"),
        ("Ш", "Sh"), ("ш", "sh"),
        ("Щ", "Sh"), ("щ", "sh"),  // rare in Uzbek
        ("Ъ", "'"),  ("ъ", "'"),   // hard sign → apostrophe
        ("Ю", "Yu"), ("ю", "yu"),
        ("Я", "Ya"), ("я", "ya"),
        // Uzbek-specific letters
        ("Ғ", "G'"), ("ғ", "g'"),
        ("Қ", "Q"),  ("қ", "q"),
        ("Ҳ", "H"),  ("ҳ", "h"),
        ("Ў", "O'"), ("ў", "o'"),
        // Basic Cyrillic
        ("А", "A"),  ("а", "a"),
        ("Б", "B"),  ("б", "b"),
        ("В", "V"),  ("в", "v"),
        ("Г", "G"),  ("г", "g"),
        ("Д", "D"),  ("д", "d"),
        ("Е", "E"),  ("е", "e"),
        ("З", "Z"),  ("з", "z"),
        ("И", "I"),  ("и", "i"),
        ("Й", "Y"),  ("й", "y"),
        ("К", "K"),  ("к", "k"),
        ("Л", "L"),  ("л", "l"),
        ("М", "M"),  ("м", "m"),
        ("Н", "N"),  ("н", "n"),
        ("О", "O"),  ("о", "o"),
        ("П", "P"),  ("п", "p"),
        ("Р", "R"),  ("р", "r"),
        ("С", "S"),  ("с", "s"),
        ("Т", "T"),  ("т", "t"),
        ("У", "U"),  ("у", "u"),
        ("Ф", "F"),  ("ф", "f"),
        ("Ь", ""),   ("ь", ""),    // soft sign → omit
        ("Ы", "I"),  ("ы", "i"),   // rare in Uzbek
        ("Э", "E"),  ("э", "e"),
    ];

    public static string ToLatin(string cyrillic)
    {
        if (string.IsNullOrEmpty(cyrillic))
            return cyrillic;

        // Use StringBuilder for efficiency on large texts
        var sb = new System.Text.StringBuilder(cyrillic.Length);
        int i = 0;
        while (i < cyrillic.Length)
        {
            bool matched = false;
            // Try two-char match first (for Ё, Ю, Я, Ш, etc.)
            foreach (var (cyr, lat) in Mappings)
            {
                if (cyrillic.Length - i >= cyr.Length &&
                    string.Compare(cyrillic, i, cyr, 0, cyr.Length, StringComparison.Ordinal) == 0)
                {
                    sb.Append(lat);
                    i += cyr.Length;
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                sb.Append(cyrillic[i]);
                i++;
            }
        }
        return sb.ToString();
    }

    // Normalize for deduplication: lowercase, collapse whitespace, remove punctuation
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lower = text.ToLowerInvariant().Trim();
        var sb = new System.Text.StringBuilder(lower.Length);
        bool prevSpace = false;
        foreach (char c in lower)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                prevSpace = false;
            }
            else if (char.IsWhiteSpace(c) && !prevSpace)
            {
                sb.Append(' ');
                prevSpace = true;
            }
        }
        return sb.ToString().Trim();
    }
}
