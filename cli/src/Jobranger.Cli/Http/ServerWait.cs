using System.Globalization;
using System.Net.Http.Headers;

namespace Jobranger.Cli.Http;

internal static class ServerWait {
    public const string ShouldRetryHeader = "x-should-retry";

    private const string millisecondsHeader = "retry-after-ms";
    private const string secondsHeader = "retry-after";
    private const double millisecondsPerSecond = 1000;

    private static readonly Dictionary<string, double> words = new(StringComparer.OrdinalIgnoreCase) {
        ["inf"] = double.PositiveInfinity,
        ["+inf"] = double.PositiveInfinity,
        ["infinity"] = double.PositiveInfinity,
        ["+infinity"] = double.PositiveInfinity,
        ["-inf"] = double.NegativeInfinity,
        ["-infinity"] = double.NegativeInfinity,
        ["nan"] = double.NaN,
        ["+nan"] = double.NaN,
        ["-nan"] = double.NaN,
    };

    public static double? Seconds(HttpResponseHeaders headers, DateTimeOffset now) {
        if (Number(Header(headers, millisecondsHeader)) is double milliseconds) {
            return milliseconds / millisecondsPerSecond;
        }
        string? seconds = Header(headers, secondsHeader);
        if (Number(seconds) is double value) {
            return value;
        }
        return RetryConditionHeaderValue.TryParse(seconds, out RetryConditionHeaderValue? condition)
            && condition.Date is DateTimeOffset date
            ? (date - now).TotalSeconds
            : null;
    }

    public static string? Header(HttpResponseHeaders headers, string name) =>
        headers.NonValidated.TryGetValues(name, out HeaderStringValues values) ? values.ToString() : null;

    private static double? Number(string? text) {
        if (text is null) {
            return null;
        }
        if (words.TryGetValue(text.Trim(), out double word)) {
            return word;
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? double.IsPositiveInfinity(value) ? double.MaxValue : value
            : null;
    }
}
