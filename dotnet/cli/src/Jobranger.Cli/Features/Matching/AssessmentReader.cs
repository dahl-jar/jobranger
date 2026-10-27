using System.Text.Json;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;
using RoleCatalogue = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Features.Matching;

internal static class AssessmentReader {
    private const string programmingKey = "is_programming_job";
    private const string labelKey = "label";
    private const string summaryKey = "summary";
    private const string masterKey = "requires_master";
    private const string titleKey = "display_title";
    private const string roleKey = "role_type";
    private const string technologiesKey = "technologies";
    private const string requirementsKey = "requirements";
    private const string checksKey = "checks";
    private const string matchKey = "match";

    private static readonly string[] assessmentKeys = [
        programmingKey, labelKey, summaryKey, masterKey, titleKey, roleKey, technologiesKey, requirementsKey, checksKey,
        matchKey,
    ];
    private static readonly string[] matchKeys = [
        StoredMatch.StatusKey, StoredMatch.ReasonKey, StoredMatch.EvidenceKey, StoredMatch.GapsKey,
        StoredMatch.BlockersKey, StoredMatch.LowCodeKey, StoredMatch.YearsKey,
    ];
    private static readonly string[] checkKeys = [StoredMatch.TextKey, StoredMatch.StateKey];
    private static readonly HashSet<string> roleTypes =
        new([.. Targets.AllRoles, RoleCatalogue.GeneralSoftware], StringComparer.Ordinal);

    public static AdAssessment? Read(string json) {
        try {
            using JsonDocument document = JsonDocument.Parse(json);
            return Assessment(document.RootElement);
        } catch (JsonException) {
            return null;
        }
    }

    private static AdAssessment? Assessment(JsonElement element) {
        Dictionary<string, JsonElement>? fields = ReplyFields.Object(element);
        if (fields is null || !ReplyFields.HasExactly(fields, assessmentKeys)) {
            return null;
        }
        bool? programming = ReplyFields.LaxFlag(fields[programmingKey]);
        string? label = Choice(fields[labelKey], Limits.AllLevels);
        string? summary = JsonText.String(fields[summaryKey]);
        bool? master = ReplyFields.LaxFlag(fields[masterKey]);
        (bool titleValid, string? title) = Optional(fields[titleKey], JsonText.String);
        (bool roleValid, string? role) = Optional(fields[roleKey], value => Choice(value, roleTypes));
        IReadOnlyList<string>? technologies = ReplyFields.Texts(fields[technologiesKey]);
        IReadOnlyList<string>? requirements = ReplyFields.Texts(fields[requirementsKey]);
        IReadOnlyList<Check>? checks = Checks(fields[checksKey]);
        MatchAssessment? match = Match(fields[matchKey]);
        if (programming is null || label is null || summary is null || master is null || !titleValid || !roleValid
            || technologies is null || requirements is null || checks is null || match is null) {
            return null;
        }
        return new AdAssessment(
            programming.Value, label, summary, master.Value, title, role, technologies, requirements, checks, match);
    }

    private static MatchAssessment? Match(JsonElement element) {
        Dictionary<string, JsonElement>? fields = ReplyFields.Object(element);
        if (fields is null || !ReplyFields.HasExactly(fields, matchKeys)) {
            return null;
        }
        string? status = Choice(fields[StoredMatch.StatusKey], Fit.All);
        string? reason = JsonText.String(fields[StoredMatch.ReasonKey]);
        IReadOnlyList<string>? evidence = ReplyFields.Texts(fields[StoredMatch.EvidenceKey]);
        IReadOnlyList<string>? gaps = ReplyFields.Texts(fields[StoredMatch.GapsKey]);
        IReadOnlyList<string>? blockers = ReplyFields.Texts(fields[StoredMatch.BlockersKey]);
        bool? lowCode = ReplyFields.LaxFlag(fields[StoredMatch.LowCodeKey]);
        (bool yearsValid, double? years) = Optional(fields[StoredMatch.YearsKey], ReplyFields.LaxNumber);
        if (status is null || reason is null || evidence is null || gaps is null || blockers is null || lowCode is null
            || !yearsValid) {
            return null;
        }
        return new MatchAssessment(status, reason, evidence, gaps, blockers, lowCode.Value, years);
    }

    private static List<Check>? Checks(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Array) {
            return null;
        }
        List<Check> checks = [];
        foreach (JsonElement item in element.EnumerateArray()) {
            Dictionary<string, JsonElement>? fields = ReplyFields.Object(item);
            if (fields is null || !ReplyFields.HasExactly(fields, checkKeys)
                || JsonText.String(fields[StoredMatch.TextKey]) is not string text
                || Choice(fields[StoredMatch.StateKey], Check.States) is not string state) {
                return null;
            }
            checks.Add(new Check(text, state));
        }
        return checks;
    }

    private static string? Choice(JsonElement element, IEnumerable<string> allowed) =>
        JsonText.String(element) is string text && allowed.Contains(text) ? text : null;

    private static (bool Valid, T? Value) Optional<T>(JsonElement element, Func<JsonElement, T?> read) {
        if (element.ValueKind == JsonValueKind.Null) {
            return (true, default);
        }
        T? value = read(element);
        return (value is not null, value);
    }
}
