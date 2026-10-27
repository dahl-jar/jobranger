using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Jobranger.Cli.Secrets;

internal static class KeyFolds {
    private const string resourceName = "Jobranger.Cli.Secrets.key-folds.json";
    private const string foldsProperty = "folds";
    private const int codeColumn = 0;
    private const int foldColumn = 1;
    private const int maxCharsPerRune = 2;

    private static readonly FrozenDictionary<int, string> folds = Load();

    public static string Fold(string text) {
        StringBuilder folded = new(text.Length);
        Span<char> chars = stackalloc char[maxCharsPerRune];
        foreach (Rune rune in text.EnumerateRunes()) {
            if (folds.TryGetValue(rune.Value, out string? ascii)) {
                folded.Append(ascii);
            } else {
                folded.Append(chars[..rune.EncodeToUtf16(chars)]);
            }
        }
        return folded.ToString();
    }

    private static FrozenDictionary<int, string> Load() {
        using Stream stream = typeof(KeyFolds).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("embedded resource missing name=" + resourceName);
        using JsonDocument document = JsonDocument.Parse(stream);
        Dictionary<int, string> rows = [];
        foreach (JsonElement row in document.RootElement.GetProperty(foldsProperty).EnumerateArray()) {
            int code = int.Parse(row[codeColumn].GetString()!, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            rows[code] = row[foldColumn].GetString()!;
        }
        return rows.ToFrozenDictionary();
    }
}
