using System.Globalization;
using System.Text;

namespace Jobranger.Messaging.Payloads;

internal static class SchemaPatterns {
    private const char escape = '\\';
    private const char classStart = '[';
    private const char classEnd = ']';
    private const char negation = '^';
    private const char rangeMark = '-';
    private const char endAnchor = '$';
    private const string inputEnd = @"\z";
    private const string classEscapes = "dDsSwW";
    private const char unicodeEscape = 'u';
    private const char hexEscape = 'x';
    private const int unicodeDigits = 4;
    private const int hexDigits = 2;
    private const int lastBmp = 0xFFFF;
    private const int firstLow = 0xDC00;
    private const int lastLow = 0xDFFF;
    private const string surrogates = @"\uD800-\uDFFF";
    private const string surrogatePair = @"[\uD800-\uDBFF][\uDC00-\uDFFF]";

    public static string ToDotNet(string pattern) {
        StringBuilder output = new();
        int index = 0;
        while (index < pattern.Length) {
            char current = pattern[index];
            if (current == escape) {
                _ = output.Append(pattern, index, 2);
                index += 2;
            } else if (current == classStart) {
                index = AppendClass(pattern, index, output);
            } else if (current == endAnchor) {
                _ = output.Append(inputEnd);
                index++;
            } else if (char.IsHighSurrogate(current)) {
                _ = output.Append("(?:").Append(pattern, index, 2).Append(')');
                index += 2;
            } else {
                _ = output.Append(current);
                index++;
            }
        }
        return output.ToString();
    }

    private static int AppendClass(string pattern, int start, StringBuilder output) {
        int index = start + 1;
        bool negated = pattern[index] == negation;
        if (negated) {
            index++;
        }
        List<(int First, int Last)> ranges = [];
        StringBuilder shorthands = new();
        while (pattern[index] != classEnd) {
            if (pattern[index] == escape && classEscapes.Contains(pattern[index + 1], StringComparison.Ordinal)) {
                _ = shorthands.Append(pattern, index, 2);
                index += 2;
                continue;
            }
            int first = ReadAtom(pattern, ref index);
            int last = first;
            if (pattern[index] == rangeMark && pattern[index + 1] != classEnd) {
                index++;
                last = ReadAtom(pattern, ref index);
            }
            ranges.Add((first, last));
        }
        int end = index + 1;
        if (ranges.All(range => range.Last <= lastBmp)) {
            _ = output.Append(pattern, start, end - start);
        } else {
            _ = output.Append(negated ? Negated(ranges, shorthands) : Positive(ranges, shorthands));
        }
        return end;
    }

    private static int ReadAtom(string pattern, ref int index) {
        char current = pattern[index];
        if (current != escape) {
            int width = char.IsHighSurrogate(current) ? 2 : 1;
            int code = char.ConvertToUtf32(pattern, index);
            index += width;
            return code;
        }
        char kind = pattern[index + 1];
        if (kind == unicodeEscape) {
            int unit = Hex(pattern, index + 2, unicodeDigits);
            index += 2 + unicodeDigits;
            if (char.IsHighSurrogate((char)unit) && pattern[index] == escape && pattern[index + 1] == unicodeEscape) {
                int low = Hex(pattern, index + 2, unicodeDigits);
                index += 2 + unicodeDigits;
                return char.ConvertToUtf32((char)unit, (char)low);
            }
            return unit;
        }
        if (kind == hexEscape) {
            index += 2 + hexDigits;
            return Hex(pattern, index - hexDigits, hexDigits);
        }
        index += 2;
        return kind;
    }

    private static int Hex(string pattern, int start, int length) =>
        int.Parse(pattern.AsSpan(start, length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static string Positive(List<(int First, int Last)> ranges, StringBuilder shorthands) {
        string bmp = BmpClass(ranges, shorthands);
        IEnumerable<string> parts = Astral(ranges);
        if (bmp.Length > 0) {
            parts = parts.Prepend($"[{bmp}]");
        }
        return $"(?:{string.Join('|', parts)})";
    }

    private static string Negated(List<(int First, int Last)> ranges, StringBuilder shorthands) {
        string astral = string.Join('|', Astral(ranges));
        string bmp = BmpClass(ranges, shorthands);
        return $"(?:(?!{astral})(?:[^{bmp}{surrogates}]|{surrogatePair}))";
    }

    private static string BmpClass(List<(int First, int Last)> ranges, StringBuilder shorthands) {
        StringBuilder bmp = new(shorthands.ToString());
        foreach ((int first, int last) in ranges.Where(range => range.First <= lastBmp)) {
            _ = bmp.Append(Unit(first));
            if (Math.Min(last, lastBmp) > first) {
                _ = bmp.Append(rangeMark).Append(Unit(Math.Min(last, lastBmp)));
            }
        }
        return bmp.ToString();
    }

    private static IEnumerable<string> Astral(List<(int First, int Last)> ranges) {
        foreach ((int first, int last) in ranges.Where(range => range.Last > lastBmp)) {
            int from = Math.Max(first, lastBmp + 1);
            string fromText = char.ConvertFromUtf32(from);
            string lastText = char.ConvertFromUtf32(last);
            char fromHigh = fromText[0];
            char lastHigh = lastText[0];
            if (fromHigh == lastHigh) {
                yield return $"{Unit(fromHigh)}[{Unit(fromText[1])}-{Unit(lastText[1])}]";
                continue;
            }
            yield return $"{Unit(fromHigh)}[{Unit(fromText[1])}-{Unit(lastLow)}]";
            if (lastHigh - fromHigh > 1) {
                yield return $"[{Unit(fromHigh + 1)}-{Unit(lastHigh - 1)}][{Unit(firstLow)}-{Unit(lastLow)}]";
            }
            yield return $"{Unit(lastHigh)}[{Unit(firstLow)}-{Unit(lastText[1])}]";
        }
    }

    private static string Unit(int code) => $@"\u{code:X4}";
}
