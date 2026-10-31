using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Jobranger.Cli.Text;

internal static class HtmlEntities {
    private const string resourceName = "entities.json";
    private const string charactersKey = "characters";
    private const char ampersand = '&';
    private const char hash = '#';
    private const char semicolon = ';';
    private const char hexMarker = 'x';
    private const string nameStops = "\t\n\f <&#;";
    private const int maxNameCodePoints = 32;
    private const int minPrefixLength = 2;
    private const int decimalBase = 10;
    private const int hexBase = 16;
    private const int tooLarge = 0x110000;
    private const int surrogateFirst = 0xD800;
    private const int surrogateLast = 0xDFFF;
    private const string replacement = "\uFFFD";

    private static readonly FrozenDictionary<string, string> names = Load();

    public static string Decode(string text) {
        if (!text.Contains(ampersand, StringComparison.Ordinal)) {
            return text;
        }
        StringBuilder decoded = new(text.Length);
        int index = 0;
        while (index < text.Length) {
            int start = text.IndexOf(ampersand, index);
            if (start < 0) {
                decoded.Append(text, index, text.Length - index);
                break;
            }
            decoded.Append(text, index, start - index);
            index = AppendReference(text, start + 1, decoded);
        }
        return decoded.ToString();
    }

    private static int AppendReference(string text, int start, StringBuilder decoded) {
        if (start < text.Length && text[start] == hash) {
            return AppendNumeric(text, start + 1, decoded);
        }
        int end = NameEnd(text, start);
        if (end == start) {
            decoded.Append(ampersand);
            return start;
        }
        if (end < text.Length && text[end] == semicolon) {
            end++;
        }
        decoded.Append(Named(text[start..end]));
        return end;
    }

    private static int AppendNumeric(string text, int start, StringBuilder decoded) {
        bool hex = start < text.Length && char.ToLowerInvariant(text[start]) == hexMarker;
        int digitsStart = hex ? start + 1 : start;
        int end = digitsStart;
        while (end < text.Length && (hex ? char.IsAsciiHexDigit(text[end]) : char.IsAsciiDigit(text[end]))) {
            end++;
        }
        if (end == digitsStart) {
            decoded.Append(ampersand);
            return start - 1;
        }
        decoded.Append(Numeric(Parse(text[digitsStart..end], hex ? hexBase : decimalBase)));
        return end < text.Length && text[end] == semicolon ? end + 1 : end;
    }

    private static int Parse(string digits, int numberBase) {
        int value = 0;
        foreach (char digit in digits) {
            value = Math.Min(value * numberBase + Uri.FromHex(digit), tooLarge);
        }
        return value;
    }

    private static string Numeric(int code) {
        if (PythonUnicode.InvalidCharref(code) is string mapped) {
            return mapped;
        }
        if (code is >= tooLarge or (>= surrogateFirst and <= surrogateLast)) {
            return replacement;
        }
        return PythonUnicode.IsDroppedCharref(code) ? string.Empty : char.ConvertFromUtf32(code);
    }

    private static int NameEnd(string text, int start) {
        int end = start;
        for (int count = 0; count < maxNameCodePoints && end < text.Length && !nameStops.Contains(text[end]); count++) {
            end += char.IsSurrogatePair(text, end) ? 2 : 1;
        }
        return end;
    }

    private static string Named(string name) {
        if (names.TryGetValue(name, out string? value)) {
            return value;
        }
        for (int length = name.Length - 1; length >= minPrefixLength; length--) {
            if (names.TryGetValue(name[..length], out string? prefix)) {
                return prefix + name[length..];
            }
        }
        return ampersand + name;
    }

    private static FrozenDictionary<string, string> Load() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject().ToFrozenDictionary(
            entity => entity.Name[1..],
            entity => entity.Value.GetProperty(charactersKey).GetString()!,
            StringComparer.Ordinal);
    }
}
