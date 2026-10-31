using System.Collections.Frozen;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Logging;

internal static partial class LogFields {
    public const string Timestamp = "ts";
    public const string Level = "level";
    public const string Logger = "logger";
    public const string Event = "event";
    public const string RunId = "run_id";
    public const string ErrorType = "error_type";
    public const string Trace = "trace";

    private const string invalidEvent = "invalid_event";
    private const int maxStringChars = 64;
    private const string notANumber = "nan";
    private const string positiveInfinity = "inf";
    private const string negativeInfinity = "-inf";
    private const char wordSeparator = '_';

    public static readonly FrozenSet<string> Allowed = FrozenSet.Create(
        StringComparer.Ordinal,
        "source", "stage", "count", "new", "active", "seen", "found_new", "checked", "total", "expired", "unknown",
        "skipped", "attempt", "delay_s", "host", "status", "error_type", "calls", "input_tokens", "output_tokens",
        "cache_read", "cache_write", "cost_usd", "duration_ms", "pages", "window_hours", "provider", "model", "size",
        "returned", "expected", "removed", "keep_days", "messages", "ad_id", "requests", "reused", "batch_id",
        "guidance_default", "about_chars", "doc_count", "file", "failure", "blocked");

    public static string EventName(string? name) =>
        name is { Length: <= maxStringChars } && EventPattern().IsMatch(name) ? name : invalidEvent;

    public static string FieldName(string key) {
        StringBuilder name = new();
        foreach (char character in key) {
            if (char.IsUpper(character) && name.Length > 0 && name[^1] != wordSeparator) {
                name.Append(wordSeparator);
            }
            name.Append(char.ToLowerInvariant(character));
        }
        return name.ToString();
    }

    public static JsonNode? Scalar(object? value) => value switch {
        string text => JsonValue.Create(Cut(text)),
        bool flag => JsonValue.Create(flag),
        int whole => JsonValue.Create(whole),
        long whole => JsonValue.Create(whole),
        decimal amount => JsonValue.Create(amount),
        double number => Number(number),
        _ => null,
    };

    public static string Cut(string text) => text.Length > maxStringChars ? text[..maxStringChars] : text;

    private static JsonValue Number(double number) =>
        double.IsNaN(number) ? JsonValue.Create(notANumber)
        : double.IsPositiveInfinity(number) ? JsonValue.Create(positiveInfinity)
        : double.IsNegativeInfinity(number) ? JsonValue.Create(negativeInfinity)
        : JsonValue.Create(number);

    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+\z")]
    private static partial Regex EventPattern();
}
