using System.Globalization;
using System.Text;

namespace Jobranger.Cli.Text;

internal static class PythonJson {
    public const string Null = "null";
    public const string ItemSeparator = ", ";
    public const string KeySeparator = ": ";

    private const string trueText = "true";
    private const string falseText = "false";
    private const char quote = '"';
    private const char firstPrintable = ' ';
    private const string unicodeEscape = "\\u";
    private const string hexFormat = "x4";
    private const string roundTrip = "R";
    private const string exponentFormat = "00";
    private const string zeroText = "0.0";
    private const string fractionZero = ".0";
    private const string zeroPoint = "0.";
    private const char exponentMark = 'E';
    private const char point = '.';
    private const char zero = '0';
    private const char minus = '-';
    private const char exponentLetter = 'e';
    private const char plus = '+';
    private const int lowestPositionalExponent = -4;
    private const int firstScientificExponent = 16;

    private static readonly Dictionary<char, string> shortEscapes = new() {
        ['"'] = "\\\"",
        ['\\'] = "\\\\",
        ['\n'] = "\\n",
        ['\r'] = "\\r",
        ['\t'] = "\\t",
        ['\b'] = "\\b",
        ['\f'] = "\\f",
    };

    public static StringBuilder AppendKey(StringBuilder json, string key, bool first) {
        if (!first) {
            json.Append(ItemSeparator);
        }
        return AppendString(json, key).Append(KeySeparator);
    }

    public static string Bool(bool value) => value ? trueText : falseText;

    public static StringBuilder AppendString(StringBuilder json, string text) {
        json.Append(quote);
        foreach (char character in text) {
            if (shortEscapes.TryGetValue(character, out string? escaped)) {
                json.Append(escaped);
            } else if (character < firstPrintable) {
                json.Append(unicodeEscape).Append(((int)character).ToString(hexFormat, CultureInfo.InvariantCulture));
            } else {
                json.Append(character);
            }
        }
        return json.Append(quote);
    }

    public static string Float(double value) {
        string shortest = Math.Abs(value).ToString(roundTrip, CultureInfo.InvariantCulture);
        string sign = double.IsNegative(value) ? minus.ToString() : string.Empty;
        int exponentAt = shortest.IndexOf(exponentMark, StringComparison.Ordinal);
        string mantissa = exponentAt < 0 ? shortest : shortest[..exponentAt];
        int shift = exponentAt < 0 ? 0 : int.Parse(shortest[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        int pointAt = mantissa.IndexOf(point, StringComparison.Ordinal);
        string digits = mantissa.Replace(point.ToString(), string.Empty, StringComparison.Ordinal);
        int position = (pointAt < 0 ? mantissa.Length : pointAt) + shift;
        string significant = digits.TrimStart(zero);
        position -= digits.Length - significant.Length;
        significant = significant.TrimEnd(zero);
        if (significant.Length == 0) {
            return sign + zeroText;
        }
        int exponent = position - 1;
        if (exponent >= lowestPositionalExponent && exponent < firstScientificExponent) {
            return sign + Positional(significant, position);
        }
        string fraction = significant.Length > 1 ? point + significant[1..] : string.Empty;
        string exponentSign = exponent < 0 ? minus.ToString() : plus.ToString();
        return sign + significant[0] + fraction + exponentLetter + exponentSign
            + Math.Abs(exponent).ToString(exponentFormat, CultureInfo.InvariantCulture);
    }

    private static string Positional(string significant, int position) {
        if (position <= 0) {
            return zeroPoint + new string(zero, -position) + significant;
        }
        if (position >= significant.Length) {
            return significant + new string(zero, position - significant.Length) + fractionZero;
        }
        return significant[..position] + point + significant[position..];
    }
}
