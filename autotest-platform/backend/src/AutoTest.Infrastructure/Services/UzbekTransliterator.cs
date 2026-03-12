using System.Text;
using AutoTest.Application.Common.Interfaces;

namespace AutoTest.Infrastructure.Services;

public class UzbekTransliterator : ITransliterationService
{
    // Cyrillic → Latin digraph mappings (order matters: check digraphs first)
    private static readonly (string Cyrillic, string Latin)[] CyrillicDigraphs =
    [
        ("Ш", "Sh"), ("ш", "sh"),
        ("Ч", "Ch"), ("ч", "ch"),
        ("Ғ", "Gʻ"), ("ғ", "gʻ"),
        ("Ў", "Oʻ"), ("ў", "oʻ"),
        ("Нг", "Ng"), ("нг", "ng"),
        ("Ё", "Yo"), ("ё", "yo"),
        ("Ю", "Yu"), ("ю", "yu"),
        ("Я", "Ya"), ("я", "ya"),
    ];

    // Single character Cyrillic → Latin mappings
    private static readonly Dictionary<char, string> CyrillicToLatinMap = new()
    {
        ['А'] = "A", ['а'] = "a",
        ['Б'] = "B", ['б'] = "b",
        ['В'] = "V", ['в'] = "v",
        ['Г'] = "G", ['г'] = "g",
        ['Д'] = "D", ['д'] = "d",
        ['Е'] = "E", ['е'] = "e",
        ['Ж'] = "J", ['ж'] = "j",
        ['З'] = "Z", ['з'] = "z",
        ['И'] = "I", ['и'] = "i",
        ['Й'] = "Y", ['й'] = "y",
        ['К'] = "K", ['к'] = "k",
        ['Л'] = "L", ['л'] = "l",
        ['М'] = "M", ['м'] = "m",
        ['Н'] = "N", ['н'] = "n",
        ['О'] = "O", ['о'] = "o",
        ['П'] = "P", ['п'] = "p",
        ['Р'] = "R", ['р'] = "r",
        ['С'] = "S", ['с'] = "s",
        ['Т'] = "T", ['т'] = "t",
        ['У'] = "U", ['у'] = "u",
        ['Ф'] = "F", ['ф'] = "f",
        ['Х'] = "X", ['х'] = "x",
        ['Ц'] = "S", ['ц'] = "s",
        ['Ъ'] = "ʼ", ['ъ'] = "ʼ",
        ['Э'] = "E", ['э'] = "e",
        ['Қ'] = "Q", ['қ'] = "q",
        ['Ҳ'] = "H", ['ҳ'] = "h",
    };

    // Latin → Cyrillic digraph mappings (order matters: check digraphs first)
    private static readonly (string Latin, string Cyrillic)[] LatinDigraphs =
    [
        ("Sh", "Ш"), ("sh", "ш"), ("SH", "Ш"),
        ("Ch", "Ч"), ("ch", "ч"), ("CH", "Ч"),
        ("Gʻ", "Ғ"), ("gʻ", "ғ"),
        ("G'", "Ғ"), ("g'", "ғ"),
        ("Oʻ", "Ў"), ("oʻ", "ў"),
        ("O'", "Ў"), ("o'", "ў"),
        ("Ng", "Нг"), ("ng", "нг"), ("NG", "НГ"),
        ("Yo", "Ё"), ("yo", "ё"), ("YO", "Ё"),
        ("Yu", "Ю"), ("yu", "ю"), ("YU", "Ю"),
        ("Ya", "Я"), ("ya", "я"), ("YA", "Я"),
    ];

    // Single character Latin → Cyrillic mappings
    private static readonly Dictionary<char, string> LatinToCyrillicMap = new()
    {
        ['A'] = "А", ['a'] = "а",
        ['B'] = "Б", ['b'] = "б",
        ['V'] = "В", ['v'] = "в",
        ['G'] = "Г", ['g'] = "г",
        ['D'] = "Д", ['d'] = "д",
        ['E'] = "Е", ['e'] = "е",
        ['J'] = "Ж", ['j'] = "ж",
        ['Z'] = "З", ['z'] = "з",
        ['I'] = "И", ['i'] = "и",
        ['Y'] = "Й", ['y'] = "й",
        ['K'] = "К", ['k'] = "к",
        ['L'] = "Л", ['l'] = "л",
        ['M'] = "М", ['m'] = "м",
        ['N'] = "Н", ['n'] = "н",
        ['O'] = "О", ['o'] = "о",
        ['P'] = "П", ['p'] = "п",
        ['R'] = "Р", ['r'] = "р",
        ['S'] = "С", ['s'] = "с",
        ['T'] = "Т", ['t'] = "т",
        ['U'] = "У", ['u'] = "у",
        ['F'] = "Ф", ['f'] = "ф",
        ['X'] = "Х", ['x'] = "х",
        ['Q'] = "Қ", ['q'] = "қ",
        ['H'] = "Ҳ", ['h'] = "ҳ",
        ['ʼ'] = "ъ",
    };

    public string CyrillicToLatin(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var matched = false;

            // Try 2-char Cyrillic digraphs first (Нг is the only true 2-char Cyrillic source)
            if (i + 1 < text.Length)
            {
                var twoChar = text.Substring(i, 2);
                foreach (var (cyrillic, latin) in CyrillicDigraphs)
                {
                    if (twoChar == cyrillic && cyrillic.Length == 2)
                    {
                        sb.Append(latin);
                        i += 2;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched)
            {
                // Try single-char digraphs (Ш→Sh, Ч→Ch, etc.)
                var ch = text[i];
                foreach (var (cyrillic, latin) in CyrillicDigraphs)
                {
                    if (cyrillic.Length == 1 && cyrillic[0] == ch)
                    {
                        sb.Append(latin);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    if (CyrillicToLatinMap.TryGetValue(ch, out var mapped))
                        sb.Append(mapped);
                    else
                        sb.Append(ch); // pass through (digits, punctuation, etc.)
                }

                i++;
            }
        }

        return sb.ToString();
    }

    public string LatinToCyrillic(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var matched = false;

            // Try 2-char Latin digraphs first
            if (i + 1 < text.Length)
            {
                var twoChar = text.Substring(i, 2);
                foreach (var (latin, cyrillic) in LatinDigraphs)
                {
                    if (latin.Length == 2 && twoChar == latin)
                    {
                        sb.Append(cyrillic);
                        i += 2;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched)
            {
                var ch = text[i];
                if (LatinToCyrillicMap.TryGetValue(ch, out var mapped))
                    sb.Append(mapped);
                else
                    sb.Append(ch);

                i++;
            }
        }

        return sb.ToString();
    }
}
