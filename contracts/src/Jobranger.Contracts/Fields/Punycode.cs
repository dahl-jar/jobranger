using System.Text;

namespace Jobranger.Contracts.Fields;

internal static class Punycode {
    private const int radix = 36;
    private const int minThreshold = 1;
    private const int maxThreshold = 26;
    private const int skew = 38;
    private const int damp = 700;
    private const int initialBias = 72;
    private const int initialCode = 0x80;
    private const int letterCount = 26;
    private const int maxCodePoint = 0x10FFFF;
    private const char delimiter = '-';
    private const int digitBase = 'a';
    private const int numberBase = '0' - letterCount;
    private const int damping = ((radix - minThreshold) * maxThreshold) / 2;

    public static string Encode(IReadOnlyList<int> codePoints) {
        StringBuilder output = new();
        foreach (int code in codePoints.Where(code => code < initialCode)) {
            _ = output.Append((char)code);
        }
        int basic = output.Length;
        int handled = basic;
        if (basic > 0) {
            _ = output.Append(delimiter);
        }
        int next = initialCode;
        long delta = 0;
        int bias = initialBias;
        while (handled < codePoints.Count) {
            int smallest = codePoints.Where(code => code >= next).Min();
            delta += (long)(smallest - next) * (handled + 1);
            next = smallest;
            foreach (int code in codePoints) {
                if (code < next) {
                    delta++;
                } else if (code == next) {
                    AppendNumber(output, delta, bias);
                    bias = Adapt(delta, handled + 1, handled == basic);
                    delta = 0;
                    handled++;
                }
            }
            delta++;
            next++;
        }
        return output.ToString();
    }

    public static string? Decode(string text) {
        int split = text.LastIndexOf(delimiter);
        List<int> output = [.. text[..Math.Max(split, 0)].Select(letter => (int)letter)];
        int index = split + 1;
        long position = 0;
        int code = initialCode;
        int bias = initialBias;
        while (index < text.Length) {
            long previous = position;
            long weight = 1;
            for (int step = radix; ; step += radix) {
                if (index >= text.Length) {
                    return null;
                }
                int digit = Digit(text[index++]);
                if (digit < 0) {
                    return null;
                }
                position += digit * weight;
                int threshold = Threshold(step, bias);
                if (digit < threshold) {
                    break;
                }
                weight *= radix - threshold;
                if (position > int.MaxValue || weight > int.MaxValue) {
                    return null;
                }
            }
            int length = output.Count + 1;
            bias = Adapt(position - previous, length, previous == 0);
            long scalar = code + (position / length);
            if (scalar > maxCodePoint || !Rune.IsValid((int)scalar)) {
                return null;
            }
            code = (int)scalar;
            position %= length;
            output.Insert((int)position, code);
            position++;
        }
        return string.Concat(output.Select(char.ConvertFromUtf32));
    }

    private static void AppendNumber(StringBuilder output, long value, int bias) {
        long rest = value;
        for (int step = radix; ; step += radix) {
            int threshold = Threshold(step, bias);
            if (rest < threshold) {
                _ = output.Append(DigitChar((int)rest));
                return;
            }
            _ = output.Append(DigitChar(threshold + (int)((rest - threshold) % (radix - threshold))));
            rest = (rest - threshold) / (radix - threshold);
        }
    }

    private static int Threshold(int step, int bias) => Math.Clamp(step - bias, minThreshold, maxThreshold);

    private static int Adapt(long delta, int length, bool first) {
        long scaled = first ? delta / damp : delta / 2;
        scaled += scaled / length;
        int shifts = 0;
        while (scaled > damping) {
            scaled /= radix - minThreshold;
            shifts += radix;
        }
        return (int)(shifts + (((radix - minThreshold + 1) * scaled) / (scaled + skew)));
    }

    private static char DigitChar(int digit) => (char)(digit < letterCount ? digit + digitBase : digit + numberBase);

    private static int Digit(char letter) => letter switch {
        >= 'a' and <= 'z' => letter - digitBase,
        >= '0' and <= '9' => letter - numberBase,
        _ => -1,
    };
}
