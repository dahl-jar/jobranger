using System.Net.Http.Headers;

namespace Jobranger.Cli.Http;

internal static class RetryAfter {
    private static readonly TimeSpan max = TimeSpan.FromSeconds(300);

    public static TimeSpan? Parse(string? header, DateTimeOffset now) {
        string value = header?.Trim() ?? "";
        if (value.Length == 0) {
            return null;
        }
        if (value.All(char.IsAsciiDigit)) {
            return long.TryParse(value, out long seconds) && seconds < max.TotalSeconds
                ? TimeSpan.FromSeconds(seconds)
                : max;
        }
        if (!RetryConditionHeaderValue.TryParse(value, out RetryConditionHeaderValue? condition)
            || condition.Date is not DateTimeOffset moment) {
            return null;
        }
        TimeSpan delay = moment - now;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > max ? max : delay;
    }
}
