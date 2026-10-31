using System.Globalization;

namespace Jobranger.Cli.Storage;

internal static class StoredTime {
    private const string secondsFormat = "yyyy-MM-dd'T'HH:mm:ss";
    private const string microsFormat = ".ffffff";
    private const string utcOffset = "+00:00";
    private const string dayFormat = "yyyy-MM-dd";

    public static string Format(DateTimeOffset time) {
        DateTime utc = time.UtcDateTime;
        string seconds = utc.ToString(secondsFormat, CultureInfo.InvariantCulture);
        bool hasMicros = utc.Ticks % TimeSpan.TicksPerSecond / TimeSpan.TicksPerMicrosecond != 0;
        string micros = hasMicros ? utc.ToString(microsFormat, CultureInfo.InvariantCulture) : "";
        return seconds + micros + utcOffset;
    }

    public static string Format(DateOnly day) => day.ToString(dayFormat, CultureInfo.InvariantCulture);
}
