using System.Globalization;
using System.Text.RegularExpressions;

namespace Jobranger.Cli.Text;

internal static partial class RustFloat {
    private const string minus = "-";
    private const string signGroup = "sign";
    private const string infinityGroup = "infinity";
    private const string nanGroup = "nan";
    private const NumberStyles decimalStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    public static double? Parse(string text) {
        Match match = Grammar().Match(text);
        if (!match.Success) {
            return null;
        }
        if (match.Groups[infinityGroup].Success) {
            return match.Groups[signGroup].Value == minus ? double.NegativeInfinity : double.PositiveInfinity;
        }
        if (match.Groups[nanGroup].Success) {
            return double.NaN;
        }
        return double.Parse(text, decimalStyles, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(
        @"^(?<sign>[+-]?)(?:(?<infinity>[iI][nN][fF](?:[iI][nN][iI][tT][yY])?)|(?<nan>[nN][aA][nN])|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex Grammar();
}
