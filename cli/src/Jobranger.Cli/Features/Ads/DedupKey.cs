using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Features.Ads;

internal static partial class DedupKey {
    private const string space = " ";
    private const char keySeparator = ':';
    private const int capitalDottedI = 0x130;
    private const int combiningDotAbove = 0x307;
    private const int capitalSigma = 0x3A3;
    private const int smallSigma = 0x3C3;
    private const int finalSigma = 0x3C2;

    private static readonly string[] companySuffixes = ["as", "asa", "sf", "ans"];
    private static readonly string smallDottedI = "i" + char.ConvertFromUtf32(combiningDotAbove);
    private static readonly HashSet<int> wordBreakMidLetters = [
        0x27, 0x2E, 0x3A, 0xB7, 0x387, 0x55F, 0x5F4, 0x2018, 0x2019, 0x2024, 0x2027, 0xFE13, 0xFE52, 0xFE55, 0xFF07,
        0xFF0E, 0xFF1A,
    ];
    private static readonly (int First, int Last)[] otherCasedRanges = [
        (0xAA, 0xAA), (0xBA, 0xBA), (0x2160, 0x217F), (0x24B6, 0x24E9), (0x1F130, 0x1F149), (0x1F150, 0x1F169),
        (0x1F170, 0x1F189),
    ];

    public static string For(string company, string title) => Normalize(company) + keySeparator + Normalize(title);

    private static string Normalize(string value) {
        List<string> words = [.. Whitespace().Replace(Lower(value), space).Trim().Split(space)];
        while (words.Count > 0 && companySuffixes.Contains(words[^1], StringComparer.Ordinal)) {
            words.RemoveAt(words.Count - 1);
        }
        return string.Join(space, words);
    }

    private static string Lower(string value) {
        Rune[] runes = [.. value.EnumerateRunes()];
        StringBuilder lower = new(value.Length);
        for (int index = 0; index < runes.Length; index++) {
            lower.Append(LowerRune(runes, index));
        }
        return lower.ToString();
    }

    private static string LowerRune(Rune[] runes, int index) => runes[index].Value switch {
        capitalDottedI => smallDottedI,
        capitalSigma => char.ConvertFromUtf32(IsFinalSigma(runes, index) ? finalSigma : smallSigma),
        _ => Rune.ToLowerInvariant(runes[index]).ToString(),
    };

    private static bool IsFinalSigma(Rune[] runes, int index) {
        bool casedBefore = FirstNotIgnorable(runes.Take(index).Reverse()) is Rune previous && IsCased(previous);
        bool casedAfter = FirstNotIgnorable(runes.Skip(index + 1)) is Rune next && IsCased(next);
        return casedBefore && !casedAfter;
    }

    private static Rune? FirstNotIgnorable(IEnumerable<Rune> runes) =>
        runes.Where(rune => !IsCaseIgnorable(rune)).Cast<Rune?>().FirstOrDefault();

    private static bool IsCased(Rune rune) =>
        Rune.GetUnicodeCategory(rune)
            is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        || otherCasedRanges.Any(range => rune.Value >= range.First && rune.Value <= range.Last);

    private static bool IsCaseIgnorable(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol
        || wordBreakMidLetters.Contains(rune.Value);

    [GeneratedRegex(@"[\s\x1c-\x1f]+")]
    private static partial Regex Whitespace();
}
