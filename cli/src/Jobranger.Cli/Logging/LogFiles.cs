using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Logging;

[UnsupportedOSPlatform("windows")]
internal static class LogFiles {
    public const int DefaultKeepDays = 14;

    private const string anyLogFile = "*" + JsonLogWriter.FileSuffix;
    private const char fieldSeparator = ' ';
    private const char valueSeparator = '=';
    private const string escapePrefix = "\\";
    private const string byteEscape = "\\x";
    private const string charEscape = "\\u";
    private const string runeEscape = "\\U";
    private const string byteDigits = "x2";
    private const string charDigits = "x4";
    private const string runeDigits = "x8";
    private const int byteEscapeLimit = 0x100;
    private const int charEscapeLimit = 0x10000;

    private static readonly Dictionary<int, string> namedEscapes = new() { ['\t'] = "t", ['\n'] = "n", ['\r'] = "r" };

    public static int Prune(string logDir, DateOnly today, int keepDays = DefaultKeepDays) {
        if (!Directory.Exists(logDir)) {
            return 0;
        }
        int removed = 0;
        foreach (string path in Directory.EnumerateFiles(logDir, anyLogFile)) {
            if (FileDay(path) is DateOnly day && today.DayNumber - day.DayNumber > keepDays) {
                File.Delete(path);
                removed++;
            }
        }
        return removed;
    }

    public static IReadOnlyList<JsonObject> ReadRun(string logDir, string runId) {
        List<JsonObject> entries = [];
        IEnumerable<string> files = Directory.Exists(logDir)
            ? Directory.EnumerateFiles(logDir, anyLogFile).Where(path => FileDay(path) is not null).Order(StringComparer.Ordinal)
            : [];
        foreach (string path in files) {
            using FileStream? stream = PrivateFile.OpenRead(path, NullLogger.Instance);
            if (stream is null) {
                continue;
            }
            using StreamReader reader = new(stream, Encoding.UTF8);
            while (reader.ReadLine() is string line) {
                if (Entry(line) is JsonObject entry && (string?)entry[LogFields.RunId] == runId) {
                    entries.Add(entry);
                }
            }
        }
        return entries;
    }

    public static string FormatLine(JsonObject entry) {
        IEnumerable<string> parts = [
            Text(entry[LogFields.Timestamp]),
            Text(entry[LogFields.Level]),
            Text(entry[LogFields.Event]),
            .. entry.Where(field => LogFields.Allowed.Contains(field.Key))
                .Select(field => field.Key + valueSeparator + Text(field.Value)),
        ];
        return Printable(string.Join(fieldSeparator, parts));
    }

    private static DateOnly? FileDay(string path) =>
        DateOnly.TryParseExact(
            Path.GetFileNameWithoutExtension(path),
            JsonLogWriter.DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly day)
            ? day
            : null;

    private static JsonObject? Entry(string line) {
        try {
            return JsonNode.Parse(line) as JsonObject;
        } catch (JsonException) {
            return null;
        }
    }

    private static string Text(JsonNode? value) => value switch {
        null => "null",
        JsonValue scalar when scalar.TryGetValue(out string? text) => text,
        _ => value.ToJsonString(),
    };

    private static string Printable(string text) {
        StringBuilder printable = new(text.Length);
        foreach (Rune rune in text.EnumerateRunes()) {
            printable.Append(IsPrintable(rune) ? rune.ToString() : Escape(rune.Value));
        }
        return printable.ToString();
    }

    private static bool IsPrintable(Rune rune) =>
        rune.Value == fieldSeparator || Rune.GetUnicodeCategory(rune) switch {
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
                or UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator => false,
            _ => true,
        };

    private static string Escape(int value) =>
        namedEscapes.TryGetValue(value, out string? name) ? escapePrefix + name
        : value < byteEscapeLimit ? byteEscape + value.ToString(byteDigits, CultureInfo.InvariantCulture)
        : value < charEscapeLimit ? charEscape + value.ToString(charDigits, CultureInfo.InvariantCulture)
        : runeEscape + value.ToString(runeDigits, CultureInfo.InvariantCulture);
}
