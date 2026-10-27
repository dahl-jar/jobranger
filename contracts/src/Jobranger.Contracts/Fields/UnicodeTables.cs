using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Jobranger.Contracts.Fields;

internal sealed class UnicodeTables {
    private const string resourceName = "idna-hosts.json";
    private const string keptKey = "ranges";
    private const string mappedKey = "mapped";
    private const string ignoredKey = "ignored";
    private const string decompositionsKey = "decompositions";
    private const string compositionsKey = "compositions";
    private const string combiningClassesKey = "combining_classes";
    private const char sequenceSeparator = ' ';

    private UnicodeTables(JsonNode document) {
        Kept = new(Rows(document, keptKey).Select(row => (Hex(row[0]), Hex(row[1]), true)));
        Mappings = new(Rows(document, mappedKey).Select(row => (Hex(row[0]), Hex(row[1]), Sequence(row[2])))
            .Concat(Rows(document, ignoredKey).Select(row => (Hex(row[0]), Hex(row[1]), Array.Empty<int>()))));
        CombiningClasses = new(Rows(document, combiningClassesKey).Select(row => (Hex(row[0]), Hex(row[1]), row[2]!.GetValue<int>())));
        Decompositions = Rows(document, decompositionsKey).ToDictionary(row => Hex(row[0]), row => Sequence(row[1]));
        Compositions = Rows(document, compositionsKey).ToDictionary(row => (Hex(row[0]), Hex(row[1])), row => Hex(row[2]));
    }

    public CodeRanges<bool> Kept { get; }

    public CodeRanges<int[]> Mappings { get; }

    public CodeRanges<int> CombiningClasses { get; }

    public IReadOnlyDictionary<int, int[]> Decompositions { get; }

    public IReadOnlyDictionary<(int First, int Second), int> Compositions { get; }

    public static UnicodeTables Load() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!;
        return new UnicodeTables(JsonNode.Parse(stream)!);
    }

    private static IEnumerable<JsonArray> Rows(JsonNode document, string key) => document[key]!.AsArray().Select(row => row!.AsArray());

    private static int Hex(JsonNode? node) => Hex(node!.GetValue<string>());

    private static int Hex(string text) => int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static int[] Sequence(JsonNode? node) => [.. node!.GetValue<string>().Split(sequenceSeparator, StringSplitOptions.RemoveEmptyEntries).Select(Hex)];
}
