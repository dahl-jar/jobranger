using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Jobranger.Cli.Storage;

internal static class JsonLists {
    private const string separator = ", ";
    private const char quote = '"';
    private const char backslash = '\\';
    private const char firstPrintable = ' ';
    private const string unicodeEscape = "\\u";
    private const string hexFormat = "x4";

    private static readonly Dictionary<char, string> shortEscapes = new() {
        ['"'] = "\\\"",
        ['\\'] = "\\\\",
        ['\n'] = "\\n",
        ['\r'] = "\\r",
        ['\t'] = "\\t",
        ['\b'] = "\\b",
        ['\f'] = "\\f",
    };

    public static string Write(IEnumerable<string> items) {
        StringBuilder json = new("[");
        bool first = true;
        foreach (string item in items) {
            if (!first) {
                json.Append(separator);
            }
            first = false;
            AppendString(json, item);
        }
        return json.Append(']').ToString();
    }

    public static IReadOnlyList<string> Read(string? stored) {
        if (stored is null) {
            return [];
        }
        try {
            using JsonDocument document = JsonDocument.Parse(stored);
            if (document.RootElement.ValueKind != JsonValueKind.Array) {
                return [];
            }
            return [.. document.RootElement.EnumerateArray().Select(ItemText)];
        } catch (Exception error) when (error is JsonException or InvalidOperationException) {
            return [];
        }
    }

    private static string ItemText(JsonElement item) =>
        item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetRawText();

    private static void AppendString(StringBuilder json, string text) {
        json.Append(quote);
        foreach (char character in text) {
            if (shortEscapes.TryGetValue(character, out string? escaped)) {
                json.Append(escaped);
            } else if (character < firstPrintable) {
                json.Append(unicodeEscape).Append(((int)character).ToString(hexFormat, CultureInfo.InvariantCulture));
            } else {
                json.Append(character);
            }
        }
        json.Append(quote);
    }
}
