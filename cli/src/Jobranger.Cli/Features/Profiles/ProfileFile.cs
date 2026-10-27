using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Profiles;

internal static partial class ProfileFile {
    public const string SummaryKey = "summary";
    public const string YearsKey = "professional_years";
    public const string HasMasterKey = "has_master";
    public const string EducationKey = "education";
    public const string ExperienceKey = "experience";
    public const string ProjectsKey = "projects";
    public const string LanguagesKey = "languages";
    public const string StackKey = "stack";
    public const string IdKey = "id";
    public const string NameKey = "name";
    public const string FactsKey = "facts";
    public const string StartKey = "start";
    public const string EndKey = "end";

    private const int factsLimit = 1200;

    private static readonly string[] profileKeys =
        [SummaryKey, YearsKey, HasMasterKey, EducationKey, ExperienceKey, ProjectsKey, LanguagesKey, StackKey];
    private static readonly string[] evidenceKeys = [IdKey, NameKey, FactsKey, StartKey, EndKey];
    private static readonly string[] evidenceLists = [EducationKey, ExperienceKey, ProjectsKey];

    public static Profile? Read(JsonNode? root, string fileName, List<ConfigError> errors) {
        if (root is not JsonObject values) {
            errors.Add(new ConfigError(fileName, ConfigProblem.NotSection));
            return null;
        }
        ConfigSection section = new(values, string.Empty, profileKeys, errors);
        string? summary = RequiredText(section, string.Empty, SummaryKey, errors);
        decimal? years = section.RequiredNumber(YearsKey);
        if (years < 0) {
            errors.Add(new ConfigError(YearsKey, ConfigProblem.BelowZero));
        }
        Dictionary<string, List<Evidence>> lists = evidenceLists.ToDictionary(
            key => key, key => EvidenceList(section, key, errors), StringComparer.Ordinal);
        CheckUniqueIds(lists, errors);
        Profile profile = new() {
            Summary = summary ?? string.Empty,
            ProfessionalYears = (double)(years ?? 0),
            HasMaster = section.Bool(HasMasterKey) ?? false,
            Education = lists[EducationKey],
            Experience = lists[ExperienceKey],
            Projects = lists[ProjectsKey],
            Languages = section.TextList(LanguagesKey) ?? [],
            Stack = section.TextList(StackKey) ?? [],
        };
        return errors.Count == 0 ? profile : null;
    }

    private static List<Evidence> EvidenceList(ConfigSection section, string key, List<ConfigError> errors) {
        List<ConfigSection> items = section.Sections(key, evidenceKeys) ?? [];
        List<Evidence> evidence = [];
        for (int index = 0; index < items.Count; index++) {
            string location = ConfigLocation.Item(key, index);
            evidence.Add(ReadEvidence(items[index], location, errors));
        }
        return evidence;
    }

    private static Evidence ReadEvidence(ConfigSection item, string location, List<ConfigError> errors) {
        string id = RequiredText(item, location, IdKey, errors) ?? string.Empty;
        if (id.Length > 0 && !EvidenceIdPattern().IsMatch(id)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, IdKey), ConfigProblem.NotEvidenceId));
        }
        string facts = RequiredText(item, location, FactsKey, errors) ?? string.Empty;
        if (CodePoints.Count(facts) > factsLimit) {
            errors.Add(new ConfigError(
                ConfigLocation.Join(location, FactsKey),
                ConfigProblem.TooLong,
                factsLimit.ToString(CultureInfo.InvariantCulture)));
        }
        return new Evidence(
            id,
            RequiredText(item, location, NameKey, errors) ?? string.Empty,
            facts,
            Month(item, location, StartKey, errors),
            Month(item, location, EndKey, errors));
    }

    private static string? Month(ConfigSection item, string location, string key, List<ConfigError> errors) {
        string? month = item.Text(key);
        if (month is not null && !MonthPattern().IsMatch(month)) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, key), ConfigProblem.NotMonth));
        }
        return month;
    }

    private static string? RequiredText(ConfigSection section, string location, string key, List<ConfigError> errors) {
        int before = errors.Count;
        string? text = section.Text(key);
        if (text is null && errors.Count == before) {
            errors.Add(new ConfigError(ConfigLocation.Join(location, key), ConfigProblem.Required));
        }
        return text;
    }

    private static void CheckUniqueIds(Dictionary<string, List<Evidence>> lists, List<ConfigError> errors) {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string key in evidenceLists) {
            List<Evidence> items = lists[key];
            for (int index = 0; index < items.Count; index++) {
                if (items[index].Id.Length > 0 && !seen.Add(items[index].Id)) {
                    errors.Add(new ConfigError(
                        ConfigLocation.Join(ConfigLocation.Item(key, index), IdKey), ConfigProblem.DuplicateKey));
                }
            }
        }
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{1,39}\z")]
    private static partial Regex EvidenceIdPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}\z")]
    private static partial Regex MonthPattern();
}
