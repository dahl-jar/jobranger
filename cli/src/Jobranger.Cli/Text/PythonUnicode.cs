using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Jobranger.Cli.Text;

internal static class PythonUnicode {
    private const string resourceName = "python-text.json";
    private const string wordKey = "word";
    private const string spaceKey = "space";
    private const string caseFoldKey = "casefold";
    private const string ignoreCaseKey = "ignore_case";
    private const string ignoreCaseLettersKey = "ignore_case_letters";
    private const string invalidCharrefsKey = "charrefs";
    private const string droppedCharrefsKey = "dropped_charrefs";
    private const string hiddenKey = "hidden";
    private const string unprintableKey = "unprintable";
    private const string nfkcDelimitersKey = "nfkc_delimiters";
    private const char sequenceSeparator = ' ';

    private static readonly CodeRanges word;
    private static readonly CodeRanges space;
    private static readonly CodeRanges droppedCharrefs;
    private static readonly CodeRanges hidden;
    private static readonly CodeRanges unprintable;
    private static readonly CodeRanges nfkcDelimiters;
    private static readonly FrozenDictionary<int, string> caseFold;
    private static readonly FrozenDictionary<int, string> invalidCharrefs;
    private static readonly FrozenDictionary<int, char> ignoreCase;
    private static readonly string ignoreCaseLetters;

    static PythonUnicode() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        word = Ranges(root, wordKey);
        space = Ranges(root, spaceKey);
        droppedCharrefs = Ranges(root, droppedCharrefsKey);
        hidden = Ranges(root, hiddenKey);
        unprintable = Ranges(root, unprintableKey);
        nfkcDelimiters = Ranges(root, nfkcDelimitersKey);
        caseFold = Mappings(root, caseFoldKey).ToFrozenDictionary(row => row.Code, row => row.Text);
        invalidCharrefs = Mappings(root, invalidCharrefsKey).ToFrozenDictionary(row => row.Code, row => row.Text);
        ignoreCase = Mappings(root, ignoreCaseKey).ToFrozenDictionary(row => row.Code, row => row.Text[0]);
        ignoreCaseLetters = root.GetProperty(ignoreCaseLettersKey).GetString()!;
    }

    public static bool IsWord(int code) => word.Contains(code);

    public static bool IsSpace(int code) => space.Contains(code);

    public static bool IsHidden(int code) => hidden.Contains(code);

    public static bool IsPrintable(int code) => !unprintable.Contains(code);

    public static bool ExpandsToUrlDelimiter(int code) => nfkcDelimiters.Contains(code);

    public static string CaseFold(string text) {
        StringBuilder folded = new(text.Length);
        foreach (int code in CodePoints.Of(text)) {
            if (caseFold.TryGetValue(code, out string? mapped)) {
                folded.Append(mapped);
            } else {
                CodePoints.Append(folded, code);
            }
        }
        return folded.ToString();
    }

    public static char? IgnoreCaseLetter(int code) {
        if (ignoreCase.TryGetValue(code, out char letter)) {
            return letter;
        }
        return code <= char.MaxValue && ignoreCaseLetters.Contains((char)code) ? (char)code : null;
    }

    public static string? InvalidCharref(int code) => invalidCharrefs.GetValueOrDefault(code);

    public static bool IsDroppedCharref(int code) => droppedCharrefs.Contains(code);

    private static CodeRanges Ranges(JsonElement root, string key) =>
        new(Rows(root, key).Select(row => (Hex(row[0]), Hex(row[1]))));

    private static IEnumerable<(int Code, string Text)> Mappings(JsonElement root, string key) =>
        Rows(root, key).Select(row => (Hex(row[0]), Sequence(row[1])));

    private static IEnumerable<string[]> Rows(JsonElement root, string key) =>
        root.GetProperty(key).EnumerateArray()
            .Select(row => row.EnumerateArray().Select(cell => cell.GetString()!).ToArray());

    private static int Hex(string text) => int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static string Sequence(string text) {
        StringBuilder decoded = new();
        foreach (string part in text.Split(sequenceSeparator)) {
            CodePoints.Append(decoded, Hex(part));
        }
        return decoded.ToString();
    }
}
