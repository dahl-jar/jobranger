using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

internal static class Ipv4Hosts {
    private const char separator = '.';
    private const int maxParts = 4;
    private const int byteValues = 256;
    private const int bitsPerByte = 8;
    private const int decimalRadix = 10;
    private const int octalRadix = 8;
    private const int hexRadix = 16;
    private const int firstLetterDigit = 10;
    private const long overflow = 1L << 40;
    private const string hexPrefix = "0x";
    private const char leadingZero = '0';
    private const string ipv4Keyword = "ipv4";

    public static bool EndsInNumber(string host) {
        string last = Parts(host)[^1];
        return (last.Length > 0 && last.All(char.IsAsciiDigit)) || Number(last) is not null;
    }

    public static string Encode(string host) {
        List<string> parts = Parts(host);
        if (parts.Count > maxParts) {
            throw ContractException.AtRoot(ipv4Keyword);
        }
        List<long> numbers = [.. parts.Select(part => Number(part) ?? throw ContractException.AtRoot(ipv4Keyword))];
        long last = numbers[^1];
        if (numbers.SkipLast(1).Any(number => number >= byteValues) || last >= 1L << (bitsPerByte * (maxParts + 1 - numbers.Count))) {
            throw ContractException.AtRoot(ipv4Keyword);
        }
        long address = last;
        for (int index = 0; index < numbers.Count - 1; index++) {
            address += numbers[index] << (bitsPerByte * (maxParts - 1 - index));
        }
        return string.Join(separator, Enumerable.Range(0, maxParts)
            .Select(index => (address >> (bitsPerByte * (maxParts - 1 - index))) & byte.MaxValue));
    }

    private static List<string> Parts(string host) {
        List<string> parts = [.. host.Split(separator)];
        if (parts.Count > 1 && parts[^1].Length == 0) {
            parts.RemoveAt(parts.Count - 1);
        }
        return parts;
    }

    private static long? Number(string text) {
        if (text.Length == 0) {
            return null;
        }
        (int radix, string digits) = text switch {
            _ when text.StartsWith(hexPrefix, StringComparison.OrdinalIgnoreCase) => (hexRadix, text[hexPrefix.Length..]),
            [leadingZero, _, ..] => (octalRadix, text[1..]),
            _ => (decimalRadix, text),
        };
        long value = 0;
        foreach (char digit in digits) {
            int digitValue = DigitValue(digit);
            if (digitValue >= radix) {
                return null;
            }
            value = Math.Min((value * radix) + digitValue, overflow);
        }
        return value;
    }

    private static int DigitValue(char digit) => digit switch {
        _ when char.IsAsciiDigit(digit) => digit - '0',
        _ when char.IsAsciiHexDigit(digit) => char.ToLowerInvariant(digit) - 'a' + firstLetterDigit,
        _ => int.MaxValue,
    };
}
