using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;

namespace Jobranger.Cli.Config.Yaml;

internal static partial class YamlCharacters {
    private const char tab = '\t';
    private const char newLine = '\n';
    private const char space = ' ';
    private const char commentMark = '#';
    private const char nextLine = '\u0085';
    private const char lineSeparator = '\u2028';
    private const char paragraphSeparator = '\u2029';
    private const char firstVisible = ' ';
    private const char lastVisible = '~';
    private const char firstWide = '\u00A0';
    private const char lastWide = '\uFFFD';
    private const char nextLineMask = '\u0080';
    private const char lineSeparatorMask = '\u0081';
    private const char paragraphSeparatorMask = '\u0082';
    private const string lonelyNextLine = " ";

    private static readonly FrozenDictionary<char, char> masks = new Dictionary<char, char> {
        [nextLine] = nextLineMask,
        [lineSeparator] = lineSeparatorMask,
        [paragraphSeparator] = paragraphSeparatorMask,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<char, char> separators =
        masks.ToFrozenDictionary(pair => pair.Value, pair => pair.Key);

    public static bool IsPrintable(char character) =>
        character is tab or newLine or nextLine
            or (>= firstVisible and <= lastVisible)
            or (>= firstWide and <= lastWide);

    public static bool IsLineSeparator(char character) => masks.ContainsKey(character);

    public static string Mask(string text) {
        StringBuilder masked = new(text);
        for (int index = 0; index < text.Length; index++) {
            if (masks.TryGetValue(text[index], out char mask)) {
                masked[index] = EndsLine(text, index + 1) ? space : mask;
            }
        }
        return masked.ToString();
    }

    public static bool EndsLine(string text, int from) {
        int next = from;
        while (next < text.Length && (text[next] == space || IsLineSeparator(text[next]))) {
            next++;
        }
        return next == text.Length || text[next] is newLine or commentMark;
    }

    public static string Unmask(string value, ScalarStyle style) =>
        SeparatorRun().Replace(value, run =>
            style == ScalarStyle.Plain && run.Index + run.Length == value.Length
                ? string.Empty
                : Fold(run.Value));

    private static string Fold(string run) {
        char[] breaks = [.. run.Where(separators.ContainsKey).Select(mask => separators[mask])];
        string first = breaks[0] switch {
            nextLine when breaks.Length == 1 => lonelyNextLine,
            nextLine => string.Empty,
            char separator => separator.ToString(),
        };
        return first + string.Concat(breaks.Skip(1).Select(item => item == nextLine ? newLine : item));
    }

    [GeneratedRegex(@"[ \t]*[\u0080-\u0082][ \t\u0080-\u0082]*", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRun();
}
