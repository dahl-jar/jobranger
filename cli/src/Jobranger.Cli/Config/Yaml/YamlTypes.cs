using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Jobranger.Cli.Text;
using YamlDotNet.Core;

namespace Jobranger.Cli.Config.Yaml;

internal static partial class YamlTypes {
    private const string intTag = "tag:yaml.org,2002:int";
    private const string floatTag = "tag:yaml.org,2002:float";
    private const string boolTag = "tag:yaml.org,2002:bool";
    private const string nullTag = "tag:yaml.org,2002:null";
    private const string nonSpecificTag = "!";
    private const string boolStarts = "tTfF";
    private const string floatStarts = "-+0123456789.";
    private const string intStarts = "-+0123456789";
    private const string mergeStarts = "<";
    private const string nullStarts = "~nN";
    private const string timestampStarts = "0123456789";
    private const string valueStarts = "=";
    private const string underscore = "_";
    private const string binaryPrefix = "0b";
    private const string octalPrefix = "0o";
    private const string hexPrefix = "0x";
    private const string infinity = ".inf";
    private const string notANumber = ".nan";
    private const char minus = '-';
    private const char plus = '+';
    private const int binaryBase = 2;
    private const int octalBase = 8;
    private const int decimalBase = 10;
    private const int hexBase = 16;
    private const int notADigit = -1;

    private static readonly YamlValue other = new(YamlKind.Other);
    private static readonly YamlValue nullValue = new(YamlKind.Null);
    private static readonly YamlValue trueValue = new(YamlKind.Bool, Number: 1);
    private static readonly YamlValue falseValue = new(YamlKind.Bool, Number: 0);
    private static readonly FrozenSet<string> trueWords = FrozenSet.Create(StringComparer.Ordinal, "yes", "y", "true", "on");
    private static readonly FrozenSet<string> falseWords = FrozenSet.Create(StringComparer.Ordinal, "no", "n", "false", "off");
    private static readonly Resolver[] resolvers = [
        new(boolStarts, BoolPattern(), value => value[0] is 't' or 'T' ? trueValue : falseValue),
        new(floatStarts, FloatPattern(), Float),
        new(intStarts, IntPattern(), Integer),
        new(mergeStarts, MergePattern(), _ => other),
        new(nullStarts, NullPattern(), _ => nullValue, TakesEmpty: true),
        new(timestampStarts, TimestampPattern(), _ => other),
        new(valueStarts, ValuePattern(), _ => other),
    ];
    private static readonly (string Prefix, int Radix)[] prefixes =
        [(binaryPrefix, binaryBase), (hexPrefix, hexBase), (octalPrefix, octalBase)];

    public static YamlValue Resolve(YamlScalar scalar) {
        if (scalar.Tag is null or nonSpecificTag) {
            return scalar.Style == ScalarStyle.Plain ? Implicit(scalar.Value) : new YamlValue(YamlKind.Text, scalar.Value);
        }
        return Explicit(scalar.Value, scalar.Tag);
    }

    private static YamlValue Implicit(string value) {
        foreach (Resolver resolver in resolvers) {
            if (resolver.Applies(value) && resolver.Pattern.IsMatch(value)) {
                return resolver.Build(value);
            }
        }
        return new YamlValue(YamlKind.Text, value);
    }

    private static YamlValue Explicit(string value, string tag) => tag switch {
        intTag => Integer(value),
        floatTag => Float(value),
        boolTag => Bool(value.ToLowerInvariant()),
        nullTag => nullValue,
        _ => other,
    };

    private static YamlValue Bool(string word) {
        if (trueWords.Contains(word)) {
            return trueValue;
        }
        return falseWords.Contains(word) ? falseValue : other;
    }

    private static YamlValue Integer(string value) {
        (bool negative, string unsigned) = Signed(value.Replace(underscore, string.Empty, StringComparison.Ordinal));
        (int radix, string digits) = Radix(unsigned);
        if (Magnitude(digits, radix) is not BigInteger magnitude) {
            return other;
        }
        double number = double.Parse(magnitude.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return new YamlValue(YamlKind.Int, Number: negative && !magnitude.IsZero ? -number : number);
    }

    private static (int Radix, string Digits) Radix(string text) {
        foreach ((string prefix, int radix) in prefixes) {
            if (text.StartsWith(prefix, StringComparison.Ordinal)) {
                return (radix, text[prefix.Length..]);
            }
        }
        return (decimalBase, text);
    }

    private static BigInteger? Magnitude(string digits, int radix) {
        if (digits.Length == 0) {
            return null;
        }
        BigInteger magnitude = BigInteger.Zero;
        foreach (char character in digits) {
            int digit = DigitValue(character);
            if (digit == notADigit || digit >= radix) {
                return null;
            }
            magnitude = (magnitude * radix) + digit;
        }
        return magnitude;
    }

    private static int DigitValue(char character) => character switch {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + decimalBase,
        >= 'A' and <= 'F' => character - 'A' + decimalBase,
        _ => notADigit,
    };

    private static YamlValue Float(string value) {
        (bool negative, string unsignedText) = Signed(
            value.Replace(underscore, string.Empty, StringComparison.Ordinal).ToLowerInvariant());
        double? number = unsignedText switch {
            infinity => double.PositiveInfinity,
            notANumber => double.NaN,
            _ => RustFloat.Parse(unsignedText),
        };
        if (number is not double parsed) {
            return other;
        }
        return new YamlValue(YamlKind.Float, Number: negative ? -parsed : parsed);
    }

    private static (bool Negative, string Unsigned) Signed(string text) {
        if (text.Length > 0 && text[0] is minus or plus) {
            return (text[0] == minus, text[1..]);
        }
        return (false, text);
    }

    [GeneratedRegex(@"^(?:true|True|TRUE|false|False|FALSE)\z", RegexOptions.CultureInvariant)]
    private static partial Regex BoolPattern();

    [GeneratedRegex(
        @"^(?:[-+]?[0-9][0-9_]*\.[0-9_]*(?:[eE][-+]?[0-9]+)?|[-+]?[0-9][0-9_]*[eE][-+]?[0-9]+|[-+]?\.[0-9_]+(?:[eE][-+][0-9]+)?|[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN))\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex FloatPattern();

    [GeneratedRegex(
        @"^(?:[-+]?0b[0-1_]+|[-+]?0o?[0-7_]+|[-+]?[0-9_]+|[-+]?0x[0-9a-fA-F_]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex IntPattern();

    [GeneratedRegex(@"^<<\z", RegexOptions.CultureInvariant)]
    private static partial Regex MergePattern();

    [GeneratedRegex(@"^=\z", RegexOptions.CultureInvariant)]
    private static partial Regex ValuePattern();

    [GeneratedRegex(@"^(?:~|null|Null|NULL|)\z", RegexOptions.CultureInvariant)]
    private static partial Regex NullPattern();

    [GeneratedRegex(
        @"^(?:[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]|[0-9][0-9][0-9][0-9]-[0-9][0-9]?-[0-9][0-9]?(?:[Tt]|[ \t]+)[0-9][0-9]?:[0-9][0-9]:[0-9][0-9](?:\.[0-9]*)?(?:[ \t]*(?:Z|[-+][0-9][0-9]?(?::[0-9][0-9])?))?)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimestampPattern();

    private sealed record Resolver(
        string FirstCharacters, Regex Pattern, Func<string, YamlValue> Build, bool TakesEmpty = false) {
        public bool Applies(string value) =>
            value.Length == 0 ? TakesEmpty : FirstCharacters.Contains(value[0], StringComparison.Ordinal);
    }
}
