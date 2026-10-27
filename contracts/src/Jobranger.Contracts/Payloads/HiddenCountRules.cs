using System.Text.Json;

namespace Jobranger.Contracts.Payloads;

internal static class HiddenCountRules {
    public const string Definition = "AdsSnapshot";

    private const string kindsDefinition = "HiddenKind";
    private const string kindsKeyword = "enum";
    private const string countsProperty = "hidden_counts";
    private const string totalProperty = "hidden_total";
    private const string hiddenProperty = "hidden";
    private const string kindProperty = "kind";
    private const string belowListed = "below_listed";

    private static readonly string[] kinds = [.. PayloadSchema.DefinitionNodes[kindsDefinition]![kindsKeyword]!.AsArray()
        .Select(kind => kind!.GetValue<string>())];

    public static SchemaFailure? Find(JsonElement snapshot) {
        if (!snapshot.GetProperty(totalProperty).TryGetInt64(out long total)) {
            return new SchemaFailure($"{ContractException.RootPath}.{totalProperty}", FailureKeywords.Type);
        }
        JsonElement counts = snapshot.GetProperty(countsProperty);
        string countsPath = $"{ContractException.RootPath}.{countsProperty}";
        Dictionary<string, long> byKind = new(StringComparer.Ordinal);
        foreach (string kind in kinds) {
            if (!counts.GetProperty(kind).TryGetInt64(out long count)) {
                return new SchemaFailure($"{countsPath}.{kind}", FailureKeywords.Type);
            }
            byKind[kind] = count;
        }
        if (byKind.Values.Sum() != total) {
            return new SchemaFailure(countsPath, totalProperty);
        }
        IEnumerable<(string Kind, int Listed)> listed = snapshot.GetProperty(hiddenProperty).EnumerateArray()
            .Select(ad => ad.GetProperty(kindProperty).GetString()!)
            .CountBy(kind => kind, StringComparer.Ordinal)
            .Select(group => (group.Key, group.Value));
        foreach ((string kind, int count) in listed) {
            if (byKind[kind] < count) {
                return new SchemaFailure($"{countsPath}.{kind}", belowListed);
            }
        }
        return null;
    }
}
