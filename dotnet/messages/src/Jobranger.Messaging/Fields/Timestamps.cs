using System.Globalization;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

public static class Timestamps {
    private const string definition = "Timestamp";
    private const string formatKeyword = "format";
    private const string format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK";
    private const int secondsEnd = 19;
    private const int maxFractionDigits = 7;

    public static DateTimeOffset Parse(string text) {
        DateTimeOffset parsed = default;
        if (!PayloadValidator.IsValid(definition, text)
            || !DateTimeOffset.TryParseExact(WithoutExtraDigits(text), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) {
            throw ContractException.AtRoot(formatKeyword);
        }
        return parsed;
    }

    private static string WithoutExtraDigits(string text) {
        int fractionStart = secondsEnd + 1;
        int fractionEnd = fractionStart;
        while (fractionEnd < text.Length && char.IsAsciiDigit(text[fractionEnd])) {
            fractionEnd++;
        }
        int extra = fractionEnd - fractionStart - maxFractionDigits;
        return extra > 0 ? text.Remove(fractionStart + maxFractionDigits, extra) : text;
    }
}
