using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Fields;

internal static class IdnaHosts {
    private const string resourceName = "idna-hosts.json";
    private const string rangesProperty = "ranges";
    private const string acePrefix = "xn--";
    private const char labelSeparator = '.';

    private static readonly (int First, int Last)[] kept = Load();
    private static readonly Comparer<(int First, int Last)> overlap = Comparer<(int First, int Last)>.Create((range, probe) =>
        range.Last < probe.First ? -1 : range.First > probe.Last ? 1 : 0);

    public static string ToAscii(string domain) =>
        string.Join(labelSeparator, domain.Split(labelSeparator).Select(Label));

    private static string Label(string label) {
        if (label.EnumerateRunes().Count() > ContractLimits.UrlMaxChars) {
            throw ContractException.AtRoot(FailureKeywords.MaxLength);
        }
        if (!Ascii.IsValid(label)) {
            return acePrefix + Punycode.Encode(Mapped(label));
        }
        string lower = label.ToLowerInvariant();
        if (!lower.StartsWith(acePrefix, StringComparison.Ordinal)) {
            return lower;
        }
        string? decoded = Punycode.Decode(lower[acePrefix.Length..]);
        if (decoded is null || Ascii.IsValid(decoded) || !decoded.EnumerateRunes().All(rune => IsKept(rune.Value) || IsLowerDnsCharacter(rune.Value))) {
            throw ContractException.AtRoot(FailureKeywords.Host);
        }
        return acePrefix + Punycode.Encode(CodePoints(decoded));
    }

    private static List<int> Mapped(string label) => [.. label.EnumerateRunes().Select(MappedCode)];

    private static int MappedCode(Rune rune) {
        if (IsKept(rune.Value)) {
            return rune.Value;
        }
        Rune lower = Rune.ToLowerInvariant(rune);
        if (rune.IsAscii || IsKept(lower.Value)) {
            return lower.Value;
        }
        throw ContractException.AtRoot(FailureKeywords.Host);
    }

    private static List<int> CodePoints(string text) => [.. text.EnumerateRunes().Select(rune => rune.Value)];

    private static bool IsLowerDnsCharacter(int code) => code is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';

    private static bool IsKept(int code) => Array.BinarySearch(kept, (code, code), overlap) >= 0;

    private static (int First, int Last)[] Load() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        return [.. JsonNode.Parse(stream)![rangesProperty]!.AsArray()
            .Select(range => (Hex(range![0]!), Hex(range[1]!)))];
    }

    private static int Hex(JsonNode node) => int.Parse(node.GetValue<string>(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}
