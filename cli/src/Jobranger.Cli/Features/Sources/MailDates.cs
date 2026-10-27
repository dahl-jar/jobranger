using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Features.Sources;

internal static partial class MailDates {
    private const int matchTimeoutMs = 1000;
    private const int centuryPivot = 68;
    private const int twentiethCentury = 1900;
    private const int twentyFirstCentury = 2000;
    private const int twoDigitYears = 100;
    private const int minutesPerHour = 60;
    private const int offsetHundreds = 100;

    private static readonly string[] monthNames = [
        "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec",
    ];

    private static readonly FrozenDictionary<string, int> zoneHours = new Dictionary<string, int> {
        ["UT"] = 0,
        ["UTC"] = 0,
        ["GMT"] = 0,
        ["Z"] = 0,
        ["AST"] = -4,
        ["ADT"] = -3,
        ["EST"] = -5,
        ["EDT"] = -4,
        ["CST"] = -6,
        ["CDT"] = -5,
        ["MST"] = -7,
        ["MDT"] = -6,
        ["PST"] = -8,
        ["PDT"] = -7,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static DateTimeOffset? Parse(string text) {
        Match match = DatePattern().Match(text);
        int month = match.Success
            ? Array.FindIndex(monthNames, name => name.Equals(match.Groups["month"].Value, StringComparison.OrdinalIgnoreCase)) + 1
            : 0;
        if (month == 0) {
            return null;
        }
        int year = Number(match.Groups["year"]);
        if (year < twoDigitYears) {
            year += year > centuryPivot ? twentiethCentury : twentyFirstCentury;
        }
        int seconds = match.Groups["second"].Success ? Number(match.Groups["second"]) : 0;
        try {
            DateTime local = new(year, month, Number(match.Groups["day"]), Number(match.Groups["hour"]),
                Number(match.Groups["minute"]), seconds, DateTimeKind.Unspecified);
            return new DateTimeOffset(local, Offset(match.Groups["zone"].Value));
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

    private static TimeSpan Offset(string zone) {
        if (zoneHours.TryGetValue(zone, out int hours)) {
            return TimeSpan.FromHours(hours);
        }
        if (NumericZone().IsMatch(zone)) {
            int value = int.Parse(zone, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int sign = Math.Sign(value);
            int magnitude = Math.Abs(value);
            return TimeSpan.FromMinutes(sign * ((magnitude / offsetHundreds * minutesPerHour) + (magnitude % offsetHundreds)));
        }
        return TimeSpan.Zero;
    }

    private static int Number(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(
        @"^\s*(?:[A-Za-z]+,?\s*)?(?<day>[0-9]{1,2})\s+(?<month>[A-Za-z]{3})[A-Za-z]*\s+(?<year>[0-9]{2,4})\s+"
        + @"(?<hour>[0-9]{1,2}):(?<minute>[0-9]{2})(?::(?<second>[0-9]{2}))?(?:\s+(?<zone>\S+))?\s*$",
        RegexOptions.None, matchTimeoutMs)]
    private static partial Regex DatePattern();

    [GeneratedRegex("^[+-][0-9]{4}$", RegexOptions.None, matchTimeoutMs)]
    private static partial Regex NumericZone();
}
