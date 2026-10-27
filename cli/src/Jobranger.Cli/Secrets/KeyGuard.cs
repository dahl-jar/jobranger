using System.Globalization;
using System.Net;
using System.Text;

namespace Jobranger.Cli.Secrets;

internal static class KeyGuard {
    public const int FragmentChars = 12;
    public const int DetailMaxChars = 300;

    private const int decodeRounds = 3;
    private const char space = ' ';

    public static string? Clean(string detail, string key) {
        string printable = Keep(detail, rune => IsPrintable(rune) || Rune.IsWhiteSpace(rune));
        if (ShowsKey(printable, key)) {
            return null;
        }
        string joined = string.Join(space, printable.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (joined.Length == 0) {
            return null;
        }
        return joined.Length > DetailMaxChars ? joined[..DetailMaxChars] : joined;
    }

    private static bool ShowsKey(string text, string key) {
        string plain = Normalize(text);
        string lowered = key.ToLowerInvariant();
        int starts = Math.Max(lowered.Length - FragmentChars + 1, 1);
        for (int start = 0; start < starts; start++) {
            string fragment = lowered.Substring(start, Math.Min(FragmentChars, lowered.Length - start));
            if (plain.Contains(fragment, StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    private static string Normalize(string text) {
        string decoded = text;
        for (int round = 0; round < decodeRounds; round++) {
            decoded = KeyFolds.Fold(WebUtility.HtmlDecode(CharRefs.Decode(Uri.UnescapeDataString(decoded))));
        }
        return Keep(decoded.ToLowerInvariant(), rune => IsPrintable(rune) && !Rune.IsWhiteSpace(rune));
    }

    private static string Keep(string text, Func<Rune, bool> keep) =>
        string.Concat(text.EnumerateRunes().Where(keep).Select(rune => rune.ToString()));

    private static bool IsPrintable(Rune rune) =>
        rune.Value == space
        || Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator);
}
