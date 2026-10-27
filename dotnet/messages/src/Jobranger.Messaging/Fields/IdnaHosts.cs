using System.Text;
using Jobranger.Messaging.Limits;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Fields;

internal static class IdnaHosts {
    private const string acePrefix = "xn--";
    private const char labelSeparator = '.';

    private static readonly UnicodeTables tables = UnicodeTables.Load();
    private static readonly Nfc nfc = new(tables);

    public static string ToAscii(string domain) {
        EnsureWithinLimit(domain.EnumerateRunes().Count());
        List<int> normalized = nfc.Normalize(domain.EnumerateRunes().SelectMany(rune => Mapped(rune.Value)));
        EnsureWithinLimit(normalized.Count);
        string text = string.Concat(normalized.Select(char.ConvertFromUtf32));
        return string.Join(labelSeparator, text.Split(labelSeparator).Select(Label));
    }

    private static void EnsureWithinLimit(int runeCount) {
        if (runeCount > MessageLimits.UrlMaxChars) {
            throw ContractException.AtRoot(FailureKeywords.MaxLength);
        }
    }

    private static int[] Mapped(int code) => tables.Mappings.TryFind(code, out int[]? mapping) ? mapping : [code];

    private static string Label(string label) {
        if (!Ascii.IsValid(label)) {
            return acePrefix + Punycode.Encode(Validated(label));
        }
        if (!label.StartsWith(acePrefix, StringComparison.Ordinal)) {
            return label;
        }
        string? decoded = Punycode.Decode(label[acePrefix.Length..]);
        if (decoded is null || !IsCanonicalAce(decoded)) {
            throw ContractException.AtRoot(FailureKeywords.Host);
        }
        return acePrefix + Punycode.Encode(CodePoints(decoded));
    }

    private static bool IsCanonicalAce(string decoded) {
        List<int> codes = CodePoints(decoded);
        return !Ascii.IsValid(decoded)
            && codes.All(code => tables.Kept.Contains(code) || IsLowerDnsCharacter(code))
            && nfc.Normalize(codes).SequenceEqual(codes);
    }

    private static List<int> Validated(string label) {
        if (!label.EnumerateRunes().All(rune => rune.IsAscii || tables.Kept.Contains(rune.Value))) {
            throw ContractException.AtRoot(FailureKeywords.Host);
        }
        return CodePoints(label);
    }

    private static List<int> CodePoints(string text) => [.. text.EnumerateRunes().Select(rune => rune.Value)];

    private static bool IsLowerDnsCharacter(int code) => code is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
}
