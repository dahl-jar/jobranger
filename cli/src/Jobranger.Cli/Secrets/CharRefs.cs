using System.Globalization;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Secrets;

internal static partial class CharRefs {
    private const string decimalGroup = "decimal";
    private const string hexGroup = "hex";
    private const string replacementChar = "\uFFFD";
    private const string windows1252 =
        "\u20AC\u0081\u201A\u0192\u201E\u2026\u2020\u2021\u02C6\u2030\u0160\u2039\u0152\u008D\u017D\u008F"
        + "\u0090\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u02DC\u2122\u0161\u203A\u0153\u009D\u017E\u0178";
    private const uint nullCode = 0x00;
    private const uint firstC1 = 0x80;
    private const uint lastC1 = 0x9F;
    private const uint firstSurrogate = 0xD800;
    private const uint lastSurrogate = 0xDFFF;
    private const uint maxCodePoint = 0x10FFFF;

    public static string Decode(string text) => CharRef().Replace(text, match => Replacement(Code(match)));

    private static uint Code(Match match) {
        Group hex = match.Groups[hexGroup];
        bool parsed = hex.Success
            ? uint.TryParse(hex.ValueSpan, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint code)
            : uint.TryParse(match.Groups[decimalGroup].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out code);
        return parsed ? code : uint.MaxValue;
    }

    private static string Replacement(uint code) => code switch {
        nullCode => replacementChar,
        >= firstC1 and <= lastC1 => windows1252[(int)(code - firstC1)..(int)(code - firstC1 + 1)],
        >= firstSurrogate and <= lastSurrogate or > maxCodePoint => replacementChar,
        _ => char.ConvertFromUtf32((int)code),
    };

    [GeneratedRegex("&#(?:(?<decimal>[0-9]+)|[xX](?<hex>[0-9a-fA-F]+));?", RegexOptions.CultureInvariant)]
    private static partial Regex CharRef();
}
