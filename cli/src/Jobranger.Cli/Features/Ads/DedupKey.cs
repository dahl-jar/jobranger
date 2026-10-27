using System.Text.RegularExpressions;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Ads;

internal static partial class DedupKey {
    private const string space = " ";
    private const char keySeparator = ':';

    public static readonly IReadOnlyList<string> CompanySuffixes = ["as", "asa", "sf", "ans"];

    public static string For(string company, string title) => Normalize(company) + keySeparator + Normalize(title);

    private static string Normalize(string value) {
        List<string> words = [.. Whitespace().Replace(PythonCase.Lower(value), space).Trim().Split(space)];
        while (words.Count > 0 && CompanySuffixes.Contains(words[^1], StringComparer.Ordinal)) {
            words.RemoveAt(words.Count - 1);
        }
        return string.Join(space, words);
    }

    [GeneratedRegex(@"[\s\x1c-\x1f]+")]
    private static partial Regex Whitespace();
}
