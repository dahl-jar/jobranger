using System.Text;
using System.Text.Json;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class StoredMatch {
    public const string StatusKey = "status";
    public const string ReasonKey = "reason";
    public const string EvidenceKey = "evidence";
    public const string GapsKey = "gaps";
    public const string BlockersKey = "blockers";
    public const string LowCodeKey = "is_low_code";
    public const string YearsKey = "required_experience_years";
    public const string TextKey = "text";
    public const string StateKey = "state";

    private const string versionKey = "version";
    private const string checksKey = "checks";

    public static string? Encode(Match? match, IReadOnlyList<Check> checks) {
        if (match is null) {
            return null;
        }
        StringBuilder json = new("{");
        PythonJson.AppendString(PythonJson.AppendKey(json, StatusKey, first: true), match.Status);
        PythonJson.AppendString(PythonJson.AppendKey(json, ReasonKey, first: false), match.Reason);
        PythonJson.AppendKey(json, EvidenceKey, first: false).Append(JsonLists.Write(match.Evidence));
        PythonJson.AppendKey(json, GapsKey, first: false).Append(JsonLists.Write(match.Gaps));
        PythonJson.AppendKey(json, BlockersKey, first: false).Append(JsonLists.Write(match.Blockers));
        PythonJson.AppendKey(json, LowCodeKey, first: false).Append(PythonJson.Bool(match.IsLowCode));
        PythonJson.AppendKey(json, YearsKey, first: false)
            .Append(match.RequiredExperienceYears is double years ? PythonJson.Float(years) : PythonJson.Null);
        PythonJson.AppendString(PythonJson.AppendKey(json, versionKey, first: false), match.Version);
        if (checks.Count > 0) {
            PythonJson.AppendKey(json, checksKey, first: false)
                .Append('[').AppendJoin(PythonJson.ItemSeparator, checks.Select(CheckObject)).Append(']');
        }
        return json.Append('}').ToString();
    }

    public static Match? Decode(string? stored) {
        if (stored is null) {
            return null;
        }
        try {
            using JsonDocument document = JsonDocument.Parse(stored);
            Dictionary<string, JsonElement>? fields = ReplyFields.Object(document.RootElement);
            if (fields is null || !fields.TryGetValue(versionKey, out JsonElement versionValue)
                || JsonText.String(versionValue) is not string version || version.Length == 0) {
                return null;
            }
            return Parse(fields, version);
        } catch (JsonException) {
            return null;
        }
    }

    private static Match? Parse(Dictionary<string, JsonElement> fields, string version) {
        JsonElement yearsValue = fields.GetValueOrDefault(YearsKey);
        double? years = ReplyFields.StrictNumber(yearsValue);
        if (years is null && yearsValue.ValueKind != JsonValueKind.Null) {
            return null;
        }
        string? status = Value(fields, StatusKey, JsonText.String);
        string? reason = Value(fields, ReasonKey, JsonText.String);
        bool? lowCode = Value(fields, LowCodeKey, ReplyFields.StrictFlag);
        IReadOnlyList<string>? evidence = Value(fields, EvidenceKey, ReplyFields.Texts);
        IReadOnlyList<string>? gaps = Value(fields, GapsKey, ReplyFields.Texts);
        IReadOnlyList<string>? blockers = Value(fields, BlockersKey, ReplyFields.Texts);
        if (status is null || reason is null || lowCode is null || evidence is null || gaps is null || blockers is null) {
            return null;
        }
        return MatchRules.Parse(
            new MatchAssessment(status, reason, evidence, gaps, blockers, lowCode.Value, years), version);
    }

    private static T? Value<T>(Dictionary<string, JsonElement> fields, string key, Func<JsonElement, T?> read) =>
        fields.TryGetValue(key, out JsonElement value) ? read(value) : default;

    private static string CheckObject(Check check) {
        StringBuilder json = new("{");
        PythonJson.AppendString(PythonJson.AppendKey(json, TextKey, first: true), check.Text);
        PythonJson.AppendString(PythonJson.AppendKey(json, StateKey, first: false), check.State);
        return json.Append('}').ToString();
    }
}
